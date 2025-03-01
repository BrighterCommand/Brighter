using System;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class SqlBinaryPayloadOutboxWritingMessageTests : IDisposable
    {
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private Message _message;
        private readonly MsSqlOutbox _sqlOutbox;
        private Message _storedMessage;
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly MessageHeader _messageHeader;

        public SqlBinaryPayloadOutboxWritingMessageTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper(binaryMessagePayload: true);
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
            _messageHeader = new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test_topic"),
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayed: TimeSpan.FromMilliseconds(5),
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("ReplyAddress"),
                contentType: "application/octet-stream",
                partitionKey: "123456789");
            _messageHeader.Bag.Add(_key1, _value1);
            _messageHeader.Bag.Add(_key2, _value2);
            _messageHeader.Bag.Add(_key3, _value3);
            _messageHeader.Bag.Add(_key4, _value4);
            _messageHeader.Bag.Add(_key5, _value5);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_MSSQL_Outbox()
        {
            _message = new Message(_messageHeader,
                new MessageBody(new byte[] { 1, 2, 3, 4, 5 }, "application/octet-stream", CharacterEncoding.Raw));
            _sqlOutbox.Add(_message, new RequestContext());

            AssertMessage();
        }

        [Fact]
        public void When_Writing_A_Message_With_a_Null_To_The_MSSQL_Outbox()
        {
            _message = new Message(_messageHeader, new MessageBody((byte[])null));
            _sqlOutbox.Add(_message, new RequestContext());

            AssertMessage();
        }

        private void AssertMessage()
        {
            _storedMessage = _sqlOutbox.Get(_message.Id, new RequestContext());

            //should read the message from the sql outbox
            Assert.Equal(_message.Body.Bytes, _storedMessage.Body.Bytes);
            //should read the header from the sql outbox
            Assert.Equal(_message.Header.Topic, _storedMessage.Header.Topic);
            Assert.Equal(_message.Header.MessageType, _storedMessage.Header.MessageType);
            Assert.Equal(_message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
            Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
            Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
            Assert.Equal(_message.Header.CorrelationId, _storedMessage.Header.CorrelationId);
            Assert.Equal(_message.Header.ReplyTo, _storedMessage.Header.ReplyTo);
            Assert.Equal(_message.Header.ContentType, _storedMessage.Header.ContentType);
            Assert.Equal(_message.Header.PartitionKey, _storedMessage.Header.PartitionKey);


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

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
