using System;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MySQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Inbox.Binary
{
    [Trait("Category", "MySql")]
    public class MySqlInboxDuplicateMessageTests : IDisposable
    {
        private readonly MySqlTestHelper _mysqlTestHelper;
        private readonly MySqlInbox _mysqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception? _exception;

        public MySqlInboxDuplicateMessageTests()
        {
            _mysqlTestHelper = new MySqlTestHelper(true);
            _mysqlTestHelper.SetupCommandDb();

            _mysqlInbox = new MySqlInbox(_mysqlTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "test-context";
            _mysqlInbox.Add(_raisedCommand, _contextKey, null, -1);
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox()
        {
            _exception = Catch.Exception(() => _mysqlInbox.Add(_raisedCommand, _contextKey, null, -1));

            //_should_succeed_even_if_the_message_is_a_duplicate
            Assert.Null(_exception);
            Assert.True(_mysqlInbox.Exists<MyCommand>(_raisedCommand.Id, _contextKey, null, -1));
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox_Different_Context()
        {
            _mysqlInbox.Add(_raisedCommand, "some other key", null, -1);

            _exception = Catch.Exception(() => _mysqlInbox.Get<MyCommand>(_raisedCommand.Id, "some other key", null, -1));

            Assert.IsType<RequestNotFoundException<MyCommand>>(_exception);
        }

        public void Dispose()
        {
            _mysqlTestHelper.CleanUpDb();
        }
    }
}
