using System;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox.Text
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxDuplicateMessageTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception? _exception;

        public SqliteInboxDuplicateMessageTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();
            _sqlInbox = new SqliteInbox(_sqliteTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "context-key";
            _sqlInbox.Add(_raisedCommand, _contextKey, null, -1);
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox()
        {
            _exception = Catch.Exception(() => _sqlInbox.Add(_raisedCommand, _contextKey, null, -1));

            //_should_succeed_even_if_the_message_is_a_duplicate
            Assert.Null(_exception);
            Assert.True(_sqlInbox.Exists<MyCommand>(_raisedCommand.Id, _contextKey, null, -1));
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
