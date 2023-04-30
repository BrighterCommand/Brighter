using System;
using FluentAssertions;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgresSql")]
    public class SqlBinaryOutboxWritingMessageTests : IDisposable
    {
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly Message _messageEarliest;
        private readonly PostgreSqlOutbox _sqlOutbox;
        private Message _storedMessage;
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;
        private readonly PostgresSqlTestHelper _postgresSqlTestHelper;

        public SqlBinaryOutboxWritingMessageTests()
        {
            _postgresSqlTestHelper = new PostgresSqlTestHelper(binaryMessagePayload: true);
            _postgresSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.Configuration);
            var messageHeader = new MessageHeader(
                messageId: Guid.NewGuid(),
                topic: "test_topic",
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayedMilliseconds: 5,
                correlationId: Guid.NewGuid(),
                replyTo: "ReplyTo",
                contentType: "text/plain",
                partitionKey: Guid.NewGuid().ToString());
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);

            _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
            _sqlOutbox.Add(_messageEarliest);
        }

        public void When_Writing_A_Message_To_A_Binary_Body_Outbox()
        {
            _storedMessage = _sqlOutbox.Get(_messageEarliest.Id);

            //should read the message from the sql outbox
            _storedMessage.Body.Value.Should().Be(_messageEarliest.Body.Value);
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

        public void Dispose()
        {
            _postgresSqlTestHelper.CleanUpDb();
        }
    }
}
