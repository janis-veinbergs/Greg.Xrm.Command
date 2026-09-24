using System.Xml;
using Greg.Xrm.Command.Commands.Views.Model;
using Greg.Xrm.Command.Commands.WebResources.PushLogic;
using Greg.Xrm.Command.Model;
using Greg.Xrm.Command.Services.Connection;
using Greg.Xrm.Command.Services.Output;

namespace Greg.Xrm.Command.Commands.Views
{
	public class SetFilterCommandExecutor(IOrganizationServiceRepository connection, IOutput output, IViewRetrieverService retriever, IPublishXmlBuilder publisher)
		: ICommandExecutor<SetFilterCommand>
	{
		public Task<CommandResult> ExecuteAsync(SetFilterCommand command, CancellationToken cancellationToken) =>
			ViewSetExecutor.RunAsync(connection, output, retriever, publisher, command,
				view => (ViewXmlEditor.SetFilter(view.fetchxml, command.Filter), null));
	}

	public class SetFetchXmlCommandExecutor(IOrganizationServiceRepository connection, IOutput output, IViewRetrieverService retriever, IPublishXmlBuilder publisher)
		: ICommandExecutor<SetFetchXmlCommand>
	{
		public Task<CommandResult> ExecuteAsync(SetFetchXmlCommand command, CancellationToken cancellationToken) =>
			ViewSetExecutor.RunAsync(connection, output, retriever, publisher, command,
				view => ViewXmlEditor.SetFetchXml(view.layoutxml, command.FetchXml, view.returnedtypecode));
	}

	public class SetColumnsCommandExecutor(IOrganizationServiceRepository connection, IOutput output, IViewRetrieverService retriever, IPublishXmlBuilder publisher)
		: ICommandExecutor<SetColumnsCommand>
	{
		public Task<CommandResult> ExecuteAsync(SetColumnsCommand command, CancellationToken cancellationToken) =>
			ViewSetExecutor.RunAsync(connection, output, retriever, publisher, command,
				view => ViewXmlEditor.SetColumns(view.fetchxml, view.layoutxml, command.Columns));
	}

	internal static class ViewSetExecutor
	{
		public static async Task<CommandResult> RunAsync(
			IOrganizationServiceRepository connection, IOutput output, IViewRetrieverService retriever,
			IPublishXmlBuilder publisher, SetViewCommand command,
			Func<TableView, (string FetchXml, string? LayoutXml)> edit)
		{
			output.Write("Connecting to the current dataverse environment...");
			var crm = await connection.GetCurrentConnectionAsync();
			output.WriteLine("Done", ConsoleColor.Green);

			var (result, view) = await retriever.GetByNameAsync(crm, command.QueryType, command.ViewName, command.TableName);
			if (view == null) return result;

			(string FetchXml, string? LayoutXml) updated;
			try
			{
				updated = edit(view);
			}
			catch (Exception ex) when (ex is ArgumentException or XmlException or InvalidOperationException)
			{
				return CommandResult.Fail($"Invalid view definition: {ex.Message}", ex);
			}

			try
			{
				output.Write($"Updating view '{view.name}'...");
				view.fetchxml = updated.FetchXml;
				if (updated.LayoutXml != null) view.layoutxml = updated.LayoutXml;
				await view.SaveOrUpdateAsync(crm);
				output.WriteLine("Done", ConsoleColor.Green);
			}
			catch (Exception ex)
			{
				output.WriteLine("Error", ConsoleColor.Red);
				return CommandResult.Fail($"An error occurred while updating the view: {ex.Message}", ex);
			}

			try
			{
				output.Write($"Publishing entity '{view.returnedtypecode}' ...");
				publisher.AddTable(view.returnedtypecode);
				await crm.ExecuteAsync(publisher.Build());
				output.WriteLine("Done", ConsoleColor.Green);
			}
			catch (Exception ex)
			{
				output.WriteLine("Error", ConsoleColor.Red);
				return CommandResult.Fail($"An error occurred while publishing the entity: {ex.Message}", ex);
			}

			return CommandResult.Success();
		}
	}
}
