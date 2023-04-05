using System;
using FluentAssertions;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class SqlBinaryPayloadOutboxWritingMessageTests
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
                messageId: Guid.NewGuid(),
                topic: "test_topic",
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayedMilliseconds: 5,
                correlationId: Guid.NewGuid(),
                replyTo: "ReplyAddress",
                contentType: "application/octet-stream");
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
            _sqlOutbox.Add(_message);

            AssertMessage();
        }

        [Fact]
        public void When_Writing_A_Message_With_a_Null_To_The_MSSQL_Outbox()
        {
            _message = new Message(_messageHeader, null);
            _sqlOutbox.Add(_message);

            AssertMessage();
        }

        private void AssertMessage()
        {
            _storedMessage = _sqlOutbox.Get(_message.Id);

            //should read the message from the sql outbox
            _storedMessage.Body.Bytes.Should().Equal(_message.Body.Bytes);
            //should read the header from the sql outbox
            _storedMessage.Header.Topic.Should().Be(_message.Header.Topic);
            _storedMessage.Header.MessageType.Should().Be(_message.Header.MessageType);
            _storedMessage.Header.TimeStamp.Should().Be(_message.Header.TimeStamp);
            _storedMessage.Header.HandledCount.Should().Be(0); // -- should be zero when read from outbox
            _storedMessage.Header.DelayedMilliseconds.Should().Be(0); // -- should be zero when read from outbox
            _storedMessage.Header.CorrelationId.Should().Be(_message.Header.CorrelationId);
            _storedMessage.Header.ReplyTo.Should().Be(_message.Header.ReplyTo);
            _storedMessage.Header.ContentType.Should().Be(_message.Header.ContentType);


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

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
