using System;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxDuplicateMessageAsyncTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception _exception;

        public SqliteInboxDuplicateMessageAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();

            _sqlInbox = new SqliteInbox(_sqliteTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand {Value = "Test"};
            _contextKey = "context-key";
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Inbox_Async()
        {
            await _sqlInbox.AddAsync(_raisedCommand, _contextKey);

            _exception = await Catch.ExceptionAsync(() => _sqlInbox.AddAsync(_raisedCommand, _contextKey));

            //_should_succeed_even_if_the_message_is_a_duplicate
            Assert.Null(_exception);
            var exists = await _sqlInbox.ExistsAsync<MyCommand>(_raisedCommand.Id, _contextKey);
            Assert.True(exists);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();

        }
    }
}
