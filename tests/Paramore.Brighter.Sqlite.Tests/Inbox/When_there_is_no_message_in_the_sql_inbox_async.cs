using System;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxEmptyWhenSearchedAsyncTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly string _contextKey;

        public SqliteInboxEmptyWhenSearchedAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();

            _sqlInbox = new SqliteInbox(_sqliteTestHelper.InboxConfiguration);
            _contextKey = "context-key";
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_Get_Async()
        {
            string commandId = Guid.NewGuid().ToString();
            var exception = await Catch.ExceptionAsync(() => _sqlInbox.GetAsync<MyCommand>(commandId, _contextKey));
            Assert.IsType<RequestNotFoundException<MyCommand>>(exception);
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_Exists_Async()
        {
            string commandId = Guid.NewGuid().ToString();
            bool exists = await _sqlInbox.ExistsAsync<MyCommand>(commandId, _contextKey);
            Assert.False(exists);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
