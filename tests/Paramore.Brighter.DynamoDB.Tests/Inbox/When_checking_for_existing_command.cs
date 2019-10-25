using System;
using FluentAssertions;
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
        private readonly Guid _guid = Guid.NewGuid();
        private string _contextKey;

        public DynamoDbCommandExistsTests()
        {                        
            _command = new MyCommand { Id = _guid, Value = "Test Earliest"};
            _contextKey = "test-context-key";

            _dynamoDbInbox = new DynamoDbInbox(Client);
            
            _dynamoDbInbox.Add(_command, _contextKey);
        }

        [Fact]
        public void When_checking_a_command_exist()
        {
            var commandExists = _dynamoDbInbox.Exists<MyCommand>(_command.Id, _contextKey);

            commandExists.Should().BeTrue("because the command exists.", commandExists);
        }

        [Fact]
        public void When_checking_a_command_exist_different_context_key()
        {
            var commandExists = _dynamoDbInbox.Exists<MyCommand>(_command.Id, "some-other-context-key");

            commandExists.Should().BeFalse("because the command exists for a different context key.", commandExists);
        }

        [Fact]
        public void When_checking_a_command_does_not_exist()
        {
            var commandExists = _dynamoDbInbox.Exists<MyCommand>(Guid.Empty, _contextKey);

            commandExists.Should().BeFalse("because the command doesn't exists.", commandExists);
        }
    }
}
