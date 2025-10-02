using System;
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox.Binary
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxAddMessageTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private MyCommand? _storedCommand;

        public SqliteInboxAddMessageTests()
        {
            _sqliteTestHelper = new SqliteTestHelper(true);
            _sqliteTestHelper.SetupCommandDb();

            _sqlInbox = new SqliteInbox(_sqliteTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "context-key";
            _sqlInbox.Add(_raisedCommand, _contextKey, null, -1);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_Inbox()
        {
            _storedCommand = _sqlInbox.Get<MyCommand>(_raisedCommand.Id, _contextKey, null, -1);

            //_should_read_the_command_from_the__sql_inbox
            Assert.NotNull(_storedCommand);
            //_should_read_the_command_value
            Assert.Equal(_raisedCommand.Value, _storedCommand.Value);
            //_should_read_the_command_id
            Assert.Equal(_raisedCommand.Id, _storedCommand.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
