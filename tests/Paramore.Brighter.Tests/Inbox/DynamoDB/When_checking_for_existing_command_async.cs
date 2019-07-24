using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.DynamoDB
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbCommandExistsAsyncTests : DynamoDBInboxBaseTest
    {       
        private readonly MyCommand _command;
        private readonly DynamoDbInbox _dynamoDbInbox;
        private readonly Guid _guid = Guid.NewGuid();
        private readonly string _contextKey;
    
        public DynamoDbCommandExistsAsyncTests()
        {                        
            _command = new MyCommand { Id = _guid, Value = "Test Earliest"};
            _contextKey = "test-context-key";

            _dynamoDbInbox = new DynamoDbInbox(new DynamoDbInboxConfiguration(Credentials, RegionEndpoint.EUWest1, TableName));
            
            _dynamoDbInbox.Add(_command, _contextKey);
        }

        [Fact]
        public async Task When_checking_a_command_exist()
        {
            var commandExists = await _dynamoDbInbox.ExistsAsync<MyCommand>(_command.Id, _contextKey);

            commandExists.Should().BeTrue("because the command exists.", commandExists);
        }

        [Fact]
        public async Task When_checking_a_command_exist_for_a_different_context()
        {
            var commandExists = await _dynamoDbInbox.ExistsAsync<MyCommand>(_command.Id, "some other context");

            commandExists.Should().BeFalse("because the command exists for a different context.", commandExists);
        }

        [Fact]
        public async Task When_checking_a_command_does_not_exist()
        {
            var commandExists = await _dynamoDbInbox.ExistsAsync<MyCommand>(Guid.Empty, _contextKey);

            commandExists.Should().BeFalse("because the command doesn't exists.", commandExists);
        }
    }
}
