using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxFetchOutstandingMessageTests : IDisposable
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly MySqlOutbox _mySqlOutbox;
        private readonly string _TopicFirstMessage = "test_topic";
        private readonly string _TopicLastMessage = "test_topic3";
        private readonly Message _message1;
        private readonly Message _message2;
        private readonly Message _messageEarliest;

        public MySqlOutboxFetchOutstandingMessageTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);

            _messageEarliest = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _TopicFirstMessage, MessageType.MT_DOCUMENT), 
                new MessageBody("message body")
            );
            _message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), "test_topic2", MessageType.MT_DOCUMENT), 
                new MessageBody("message body2")
            );
            _message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _TopicLastMessage, MessageType.MT_DOCUMENT), 
                new MessageBody("message body3")
            );
            _mySqlOutbox.Add(_messageEarliest);
            Thread.Sleep(100);
            _mySqlOutbox.Add(_message1);
            Thread.Sleep(100);
            _mySqlOutbox.Add(_message2);

            // Not sure why (assuming time skew) but needs time to settle
            Thread.Sleep(7000);
        }

        [Fact]
        public void When_there_are_multiple_outstanding_messages_in_the_outbox_and_messages_within_an_interval_are_fetched()
        {
            var messages = _mySqlOutbox.OutstandingMessages(millSecondsSinceSent: 0);

            messages.Should().NotBeNullOrEmpty();

            messages.Should().HaveCount(3);
        }
        
        [Fact]
        public async Task When_there_are_multiple_outstanding_messages_in_the_outbox_and_messages_within_an_interval_are_fetched_async()
        {
            var messages = await _mySqlOutbox.OutstandingMessagesAsync(millSecondsSinceSent: 0);

            messages.Should().NotBeNullOrEmpty();

            messages.Should().HaveCount(3);
        }

        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
