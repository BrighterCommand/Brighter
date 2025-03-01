using System.Threading.Tasks;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Inbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbInboxAddMessageAsyncTests : DynamoDBInboxBaseTest
    {
        private readonly DynamoDbInbox _dynamoDbInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private MyCommand _storedCommand;

        public DynamoDbInboxAddMessageAsyncTests()
        {
            _dynamoDbInbox = new DynamoDbInbox(Client, new DynamoDbInboxConfiguration());

            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "context-key";
            _dynamoDbInbox.Add(_raisedCommand, _contextKey);
        }

        [Fact]
        public async Task When_writing_a_message_to_the_inbox()
        {
            _storedCommand = await _dynamoDbInbox.GetAsync<MyCommand>(_raisedCommand.Id, _contextKey);

            //Should read the command from the dynamo_db inbox
            Assert.NotNull(_storedCommand);
            //Should read the command value
            Assert.Equal(_raisedCommand.Value, _storedCommand.Value);
            //Should read the command id
            Assert.Equal(_raisedCommand.Id, _storedCommand.Id);
        }
    }
}
