#!/usr/bin/env pwsh
<#
.SYNOPSIS
Builds, packs and installs/updates the pacx global tool from local sources.

.DESCRIPTION
Version follows the CI scheme 1.<year>.<month>.<revision>. The revision is
one higher than the highest revision found in the installed tool, the local
package folder and nuget.org, so the local build always wins over them.

.PARAMETER Version
Explicit version to use instead of the computed one.
#>
param(
	[string] $Version
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$packageId = 'Greg.Xrm.Command'
$project = 'Greg.Xrm.Command/Greg.Xrm.Command.csproj'
$outputDir = Join-Path $repoRoot '.artifacts/pacx'

# Returns the 4th component of every version found in the given strings
function Get-Revisions([string[]] $text) {
	$text | Select-String -Pattern '\d+\.\d+\.\d+\.(\d+)' -AllMatches |
		ForEach-Object { $_.Matches } |
		ForEach-Object { [int]$_.Groups[1].Value }
}

function Get-InstalledTool {
	dotnet tool list --global | Where-Object { $_ -match "^$([regex]::Escape($packageId))\s" }
}

function Get-NextVersion {
	$revisions = @(0)
	$revisions += Get-Revisions (Get-InstalledTool)

	if (Test-Path $outputDir) {
		$revisions += Get-Revisions (Get-ChildItem $outputDir -Filter '*.nupkg').Name
	}

	try {
		$index = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$($packageId.ToLower())/index.json" -TimeoutSec 5
		$revisions += Get-Revisions $index.versions
	}
	catch {
		Write-Warning "Could not query nuget.org: $($_.Exception.Message)"
	}

	$max = ($revisions | Measure-Object -Maximum).Maximum
	$now = Get-Date
	"1.$($now.Year).$($now.Month).$($max + 1)"
}

if (-not $Version) { $Version = Get-NextVersion }
Write-Host "==> Version $Version" -ForegroundColor Cyan

Write-Host '==> Build' -ForegroundColor Cyan
dotnet build Greg.Xrm.Command.sln -c Release --no-incremental -p:Version=$Version

Write-Host '==> Pack' -ForegroundColor Cyan
dotnet pack $project -c Release --no-build -p:Version=$Version -o $outputDir

Write-Host '==> Install' -ForegroundColor Cyan
$verb = if (Get-InstalledTool) { 'update' } else { 'install' }
dotnet tool $verb --global $packageId --version $Version --add-source $outputDir

Write-Host "==> pacx $Version installed" -ForegroundColor Green
