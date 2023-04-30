using System;
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests
{
    public class MySqlOutboxWritingBinaryMessageTests
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly MySqlOutbox _mySqlOutbox;
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly Message _messageEarliest;
        private Message _storedMessage;
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;

        public MySqlOutboxWritingBinaryMessageTests()
        {
            _mySqlTestHelper = new MySqlTestHelper(binaryMessagePayload: true);
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);
            var messageHeader = new MessageHeader(
                messageId: Guid.NewGuid(),
                topic: "test_topic",
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayedMilliseconds: 5,
                correlationId: new Guid(),
                replyTo: "ReplyTo",
                contentType: "application/octet-stream",
                partitionKey: Guid.NewGuid().ToString());
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);

            _messageEarliest = new Message(
                messageHeader, 
                new MessageBody(new byte[] { 1, 2, 3, 4, 5 }, "application/octet-stream", CharacterEncoding.Raw )
                );
            _mySqlOutbox.Add(_messageEarliest);
        }

        [Fact]
        public void When_writing_a_message_to_a_binary_body_outbox()
        {
            _storedMessage = _mySqlOutbox.Get(_messageEarliest.Id);

            //should read the message from the sql outbox
            _storedMessage.Body.Bytes.Should().Equal(_messageEarliest.Body.Bytes);
            
            //should read the header from the sql outbox
            _storedMessage.Header.Topic.Should().Be(_messageEarliest.Header.Topic);
            _storedMessage.Header.MessageType.Should().Be(_messageEarliest.Header.MessageType);
            _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                .Should().Be(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            _storedMessage.Header.HandledCount.Should().Be(0); // -- should be zero when read from outbox
            _storedMessage.Header.DelayedMilliseconds.Should().Be(0); // -- should be zero when read from outbox
            _storedMessage.Header.CorrelationId.Should().Be(_messageEarliest.Header.CorrelationId);
            _storedMessage.Header.ReplyTo.Should().Be(_messageEarliest.Header.ReplyTo);
            _storedMessage.Header.ContentType.Should().Be(_messageEarliest.Header.ContentType);
            _storedMessage.Header.PartitionKey.Should().Be(_messageEarliest.Header.PartitionKey);

            //Bag serialization
            _storedMessage.Header.Bag.ContainsKey(_key1).Should().BeTrue();
            _storedMessage.Header.Bag[_key1].Should().Be(_value1);
            _storedMessage.Header.Bag.ContainsKey(_key2).Should().BeTrue();
            _storedMessage.Header.Bag[_key2].Should().Be(_value2);
            _storedMessage.Header.Bag.ContainsKey(_key3).Should().BeTrue();
            _storedMessage.Header.Bag[_key3].Should().Be(_value3);
            _storedMessage.Header.Bag.ContainsKey(_key4).Should().BeTrue();
            _storedMessage.Header.Bag[_key4].Should().Be(_value4);
            _storedMessage.Header.Bag.ContainsKey(_key5).Should().BeTrue();
            _storedMessage.Header.Bag[_key5].Should().Be(_value5);
        }
    }
}
