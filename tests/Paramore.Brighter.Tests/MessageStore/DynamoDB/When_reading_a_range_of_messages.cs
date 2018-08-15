using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessageStore.DynamoDB
{
    [Trait("Category", "DynamoDB")]
    [Collection("DynamoDB MessageStore")]
    public class DynamoDbRangeOfMessagesTests : BaseDynamoDBMessageStoreTests
    {
        private readonly Guid[] _guids;
        private readonly Message _messageEarliest, _message2, _messageLatest, _nonTopicMessage;
        private readonly string _topic = "Test";
        private readonly DateTime _timeStamp = new DateTime(2018, 7, 5, 12, 0, 0);

        public DynamoDbRangeOfMessagesTests()
        {
            _guids = new[] {Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()};

            _messageEarliest =
                new Message(new MessageHeader(_guids[0], _topic, MessageType.MT_COMMAND, _timeStamp.AddHours(-4)), new MessageBody("Body"));
            _message2 = new Message(
                new MessageHeader(_guids[1], _topic, MessageType.MT_COMMAND, _timeStamp.AddHours(-2)), new MessageBody("Body2"));
            _messageLatest =
                new Message(new MessageHeader(_guids[2], _topic, MessageType.MT_COMMAND, _timeStamp.AddHours(-1)), new MessageBody("Body3"));
            _nonTopicMessage = 
                new Message(new MessageHeader(_guids[3], "Test2", MessageType.MT_COMMAND, _timeStamp.AddHours(-2)), new MessageBody("Body 4"));

            _dynamoDbMessageStore.Add(_messageEarliest);
            _dynamoDbMessageStore.Add(_message2);
            _dynamoDbMessageStore.Add(_messageLatest);
            _dynamoDbMessageStore.Add(_nonTopicMessage);
        }

        [Fact]
        public void When_reading_messages_by_numerical_range()
        {
            var exception = Catch.Exception(() => _dynamoDbMessageStore.Get(3, 1));
            exception.Should().BeOfType<NotSupportedException>();
        }

        [Fact]
        public void When_reading_message_by_time_range()
        {
            var retrievedMessages = _dynamoDbMessageStore.Get(_topic, _timeStamp, _timeStamp.AddHours(-3), _timeStamp.AddHours(-2));

            //_should_read_the_last_middle_message_from_the_store
            retrievedMessages.Should().HaveCount(1);
            retrievedMessages.Single().Should().Be(_message2);
        }

        [Fact]
        public void When_reading_message_from_time()
        {
            var retrievedMessages = _dynamoDbMessageStore.Get(_topic, _timeStamp, _timeStamp.AddHours(-2));

            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(2);
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[1]).Should().NotBeNull();
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[2]).Should().NotBeNull();
        }

        [Fact]
        public void When_reading_message_until_time()
        {
            var retrievedMessages = _dynamoDbMessageStore.Get(_topic, _timeStamp, endTime: _timeStamp.AddHours(-2));

            //_should_read_the_last_two_messages_from_the_store
            retrievedMessages.Should().HaveCount(2);
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[0]).Should().NotBeNull();
            retrievedMessages.FirstOrDefault(m => m.Id == _guids[1]).Should().NotBeNull();
        }
    }
}
