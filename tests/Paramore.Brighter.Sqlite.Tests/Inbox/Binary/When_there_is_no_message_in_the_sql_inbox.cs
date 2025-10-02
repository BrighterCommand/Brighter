using System;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox.Binary
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxEmptyWhenSearchedTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly string _contextKey;

        public SqliteInboxEmptyWhenSearchedTests()
        {
            _sqliteTestHelper = new SqliteTestHelper(true);
            _sqliteTestHelper.SetupCommandDb();
            _sqlInbox = new SqliteInbox(_sqliteTestHelper.InboxConfiguration);
            _contextKey = "context-key";
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Inbox_Get()
        {
            string commandId = Guid.NewGuid().ToString();
            var exception = Catch.Exception(() => _sqlInbox.Get<MyCommand>(commandId, _contextKey, null, -1));
            Assert.IsType<RequestNotFoundException<MyCommand>>(exception);
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Inbox_Exists()
        {
            string commandId = Guid.NewGuid().ToString();
            Assert.False(_sqlInbox.Exists<MyCommand>(commandId, _contextKey, null, -1));
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
