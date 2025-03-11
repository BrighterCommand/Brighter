using System;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Inbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbCommandExistsTests : DynamoDBInboxBaseTest
    {
        private readonly MyCommand _command;

        private readonly DynamoDbInbox _dynamoDbInbox;
        private string _contextKey;

        public DynamoDbCommandExistsTests()
        {
            _command = new MyCommand { Id = Guid.NewGuid().ToString(), Value = "Test Earliest"};
            _contextKey = "test-context-key";

            _dynamoDbInbox = new DynamoDbInbox(Client, new DynamoDbInboxConfiguration());

            _dynamoDbInbox.Add(_command, _contextKey);
        }

        [Fact]
        public void When_checking_a_command_exist()
        {
            var commandExists = _dynamoDbInbox.Exists<MyCommand>(_command.Id, _contextKey);

            Assert.True(commandExists);
        }

        [Fact]
        public void When_checking_a_command_exist_different_context_key()
        {
            var commandExists = _dynamoDbInbox.Exists<MyCommand>(_command.Id, "some-other-context-key");

            Assert.False(commandExists);
        }

        [Fact]
        public void When_checking_a_command_does_not_exist()
        {
            var commandExists = _dynamoDbInbox.Exists<MyCommand>(Guid.NewGuid().ToString(), _contextKey);

            Assert.False(commandExists);
        }
    }
}
