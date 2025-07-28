using System;
using System.Net.Mime;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
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
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test_topic"),
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayed: TimeSpan.FromMilliseconds(5),
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("ReplyTo"),
                contentType: new ContentType(MediaTypeNames.Application.Octet),
                partitionKey: Guid.NewGuid().ToString());
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);

            _messageEarliest = new Message(
                messageHeader, 
                new MessageBody([1, 2, 3, 4, 5], new ContentType(MediaTypeNames.Application.Octet), CharacterEncoding.Raw )
                );
            _mySqlOutbox.Add(_messageEarliest, new RequestContext());
        }

        [Fact]
        public void When_writing_a_message_to_a_binary_body_outbox()
        {
            _storedMessage = _mySqlOutbox.Get(_messageEarliest.Id, new RequestContext());

            //should read the message from the sql outbox
            Assert.Equal(_messageEarliest.Body.Bytes, _storedMessage.Body.Bytes);
            
            //should read the header from the sql outbox
            Assert.Equal(_messageEarliest.Header.Topic, _storedMessage.Header.Topic);
            Assert.Equal(_messageEarliest.Header.MessageType, _storedMessage.Header.MessageType);
            Assert.Equal(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
            Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
            Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
            Assert.Equal(_messageEarliest.Header.CorrelationId, _storedMessage.Header.CorrelationId);
            Assert.Equal(_messageEarliest.Header.ReplyTo, _storedMessage.Header.ReplyTo);
            Assert.Equal(_messageEarliest.Header.ContentType, _storedMessage.Header.ContentType);
            Assert.Equal(_messageEarliest.Header.PartitionKey, _storedMessage.Header.PartitionKey);

            //Bag serialization
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key1));
            Assert.Equal(_value1, _storedMessage.Header.Bag[_key1]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key2));
            Assert.Equal(_value2, _storedMessage.Header.Bag[_key2]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key3));
            Assert.Equal(_value3, _storedMessage.Header.Bag[_key3]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key4));
            Assert.Equal(_value4, _storedMessage.Header.Bag[_key4]);
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key5));
            Assert.Equal(_value5, _storedMessage.Header.Bag[_key5]);
        }
    }
}
