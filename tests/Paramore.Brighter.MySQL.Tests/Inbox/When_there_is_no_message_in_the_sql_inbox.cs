using System;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MySQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Inbox
{
    [Trait("Category", "MySql")]
    public class SqlInboxEmptyWhenSearchedTests : IDisposable
    {
        private readonly MySqlTestHelper _mysqlTestHelper;
        private readonly MySqlInbox _mysqlInBox;
        private readonly string _contextKey;

        public SqlInboxEmptyWhenSearchedTests()
        {
            _mysqlTestHelper = new MySqlTestHelper();
            _mysqlTestHelper.SetupCommandDb();

            _mysqlInBox = new MySqlInbox(_mysqlTestHelper.InboxConfiguration);
            _contextKey = "test-context";
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Inbox_Get()
        {
            string commandId = Guid.NewGuid().ToString();
            var exception = Catch.Exception(() => _mysqlInBox.Get<MyCommand>(commandId, _contextKey, null));
            Assert.IsType<RequestNotFoundException<MyCommand>>(exception);
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Inbox_Exists()
        {
            string commandId = Guid.NewGuid().ToString();
            Assert.False(_mysqlInBox.Exists<MyCommand>(commandId, _contextKey, null));
        }

        public void Dispose()
        {
            _mysqlTestHelper.CleanUpDb();
        }
    }
}
