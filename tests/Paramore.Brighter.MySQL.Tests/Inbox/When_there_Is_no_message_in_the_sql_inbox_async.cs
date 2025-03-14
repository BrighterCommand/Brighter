using System;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MySQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Inbox
{
    [Trait("Category", "MySql")]
    public class  SqlInboxEmptyWhenSearchedAsyncTests : IDisposable
    {
        private readonly MySqlTestHelper _mysqlTestHelper;
        private readonly MySqlInbox _mysqlInbox;
        private readonly string _contextKey;

        public SqlInboxEmptyWhenSearchedAsyncTests()
        {
            _mysqlTestHelper = new MySqlTestHelper();
            _mysqlTestHelper.SetupCommandDb();

            _mysqlInbox = new MySqlInbox(_mysqlTestHelper.InboxConfiguration);
            _contextKey = "test-context";
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_Get_Async()
        {
            string commandId = Guid.NewGuid().ToString();
            var exception = await Catch.ExceptionAsync(() => _mysqlInbox.GetAsync<MyCommand>(commandId, _contextKey, null));
            Assert.IsType<RequestNotFoundException<MyCommand>>(exception);
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_Exists_Async()
        {
            string commandId = Guid.NewGuid().ToString();
            bool exists = await _mysqlInbox.ExistsAsync<MyCommand>(commandId, _contextKey, null);
            Assert.False(exists);
        }

        public void Dispose()
        {
            _mysqlTestHelper.CleanUpDb();
        }
    }
}
