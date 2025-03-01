using System;
using System.Threading.Tasks;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Inbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbCommandExistsAsyncTests : DynamoDBInboxBaseTest
    {
        private readonly MyCommand _command;
        private readonly DynamoDbInbox _dynamoDbInbox;
        private readonly string _contextKey;

        public DynamoDbCommandExistsAsyncTests()
        {
            _command = new MyCommand { Id = Guid.NewGuid().ToString(), Value = "Test Earliest"};
            _contextKey = "test-context-key";

            _dynamoDbInbox = new DynamoDbInbox(Client, new DynamoDbInboxConfiguration());

            _dynamoDbInbox.Add(_command, _contextKey);
        }

        [Fact]
        public async Task When_checking_a_command_exist()
        {
            var commandExists = await _dynamoDbInbox.ExistsAsync<MyCommand>(_command.Id, _contextKey);

            Assert.True(commandExists);
        }

        [Fact]
        public async Task When_checking_a_command_exist_for_a_different_context()
        {
            var commandExists = await _dynamoDbInbox.ExistsAsync<MyCommand>(_command.Id, "some other context");

            Assert.False(commandExists);
        }

        [Fact]
        public async Task When_checking_a_command_does_not_exist()
        {
            var commandExists = await _dynamoDbInbox.ExistsAsync<MyCommand>(Guid.NewGuid().ToString(), _contextKey);

            Assert.False(commandExists);
        }
    }
}
