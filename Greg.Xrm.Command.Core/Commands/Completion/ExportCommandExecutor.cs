using Greg.Xrm.Command.Parsing;
using Newtonsoft.Json;
using Spectre.Console;

namespace Greg.Xrm.Command.Commands.Completion
{
	public class ExportCommandExecutor(
		IAnsiConsole ansiConsole,
		ICommandRegistry registry
		) : ICommandExecutor<ExportCommand>
	{
		public Task<CommandResult> ExecuteAsync(ExportCommand command, CancellationToken cancellationToken)
		{
			var commands = registry.Commands
				.Where(c => !c.Hidden && !IsUnderHiddenNamespace(registry.Tree, c.Verbs))
				.OrderBy(c => c)
				.Select(c => new
				{
					verbs = c.Verbs,
					aliases = c.Aliases.Select(a => a.Verbs).ToArray(),
					help = c.HelpText,
					options = c.Options.Select(o => new
					{
						@long = o.Option.LongName,
						@short = o.Option.ShortName,
						help = o.Option.HelpText,
						required = o.IsRequired,
						@default = o.Option.DefaultValue?.ToString(),
						values = GetEnumValues(o)
					}).ToArray()
				})
				.ToArray();

			var namespaces = new List<object>();
			CollectNamespaces(registry.Tree, new List<string>(), namespaces);

			// non-ASCII characters are escaped so the output survives redirection:
			// with stdout redirected the console code page can turn e.g. "→" in a
			// help text into a control character, which is invalid inside JSON
			var json = JsonConvert.SerializeObject(new { commands, namespaces }, new JsonSerializerSettings
			{
				Formatting = Formatting.Indented,
				StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
			});

			// written through the raw output writer on purpose: the ansi console
			// renderer would hard-wrap long lines at the console width, which
			// breaks consumers that parse the JSON (e.g. the completion scripts)
			ansiConsole.Profile.Out.Writer.WriteLine(json);

			return Task.FromResult(CommandResult.Success());
		}


		/// <summary>
		/// Commands under a hidden namespace (e.g. "!config") are not shown by the help,
		/// so they should not be offered by the completion either.
		/// </summary>
		private static bool IsUnderHiddenNamespace(IReadOnlyList<VerbNode> tree, IReadOnlyList<string> verbs)
		{
			var nodes = tree;
			foreach (var verb in verbs)
			{
				var node = nodes.FirstOrDefault(n => string.Equals(n.Verb, verb, StringComparison.OrdinalIgnoreCase));
				if (node is null) return false;
				if (node.IsHidden) return true;
				nodes = node.Children;
			}
			return false;
		}


		private static string[]? GetEnumValues(OptionDefinition optionDefinition)
		{
			var enumType = optionDefinition.Property.PropertyType.GetEnumType();
			if (enumType == null || optionDefinition.Option.SuppressValuesHelp)
				return null;

			return Enum.GetNames(enumType);
		}


		private static void CollectNamespaces(IReadOnlyList<VerbNode> nodes, List<string> path, List<object> result)
		{
			foreach (var node in nodes)
			{
				if (node.IsHidden) continue;
				if (node.Children.Count == 0) continue;

				var currentPath = new List<string>(path) { node.Verb };
				result.Add(new
				{
					verbs = currentPath.ToArray(),
					help = node.Help
				});

				CollectNamespaces(node.Children, currentPath, result);
			}
		}
	}
}
