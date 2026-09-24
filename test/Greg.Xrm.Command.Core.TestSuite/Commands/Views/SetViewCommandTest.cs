namespace Greg.Xrm.Command.Commands.Views
{
	[TestClass]
	public class SetViewCommandTest
	{
		[TestMethod]
		public void ParseSetFilter()
		{
			var command = Utility.TestParseCommand<SetFilterCommand>("view", "setFilter", "--name", "My View", "--table", "account", "--filter", "<filter/>");
			Assert.AreEqual("My View", command.ViewName);
			Assert.AreEqual("account", command.TableName);
			Assert.AreEqual("<filter/>", command.Filter);
		}

		[TestMethod]
		public void ParseSetFetchXml()
		{
			var command = Utility.TestParseCommand<SetFetchXmlCommand>("view", "setFetchXml", "-n", "My View", "-f", "<fetch/>");
			Assert.AreEqual("My View", command.ViewName);
			Assert.AreEqual("<fetch/>", command.FetchXml);
		}

		[TestMethod]
		public void ParseSetColumns()
		{
			var command = Utility.TestParseCommand<SetColumnsCommand>("view", "setColumns", "--name", "My View", "--columns", "name,telephone1");
			Assert.AreEqual("My View", command.ViewName);
			Assert.AreEqual("name,telephone1", command.Columns);
		}
	}
}
