using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxFetchOutstandingMessageTests : IDisposable
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly MySqlOutbox _mySqlOutbox;
        private readonly RoutingKey _routingKeyOne = new("test_topic");
        private readonly RoutingKey _routingKeyTwo = new("test_topic2"); 
        private readonly RoutingKey _routingKeyThree = new("test_topic3");
        private readonly RequestContext _context;

        public MySqlOutboxFetchOutstandingMessageTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);
            _context = new RequestContext();

            Message messageEarliest = new(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKeyOne, MessageType.MT_DOCUMENT), 
                new MessageBody("message body")
            );
            Message message1 = new(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKeyTwo, MessageType.MT_DOCUMENT), 
                new MessageBody("message body2")
            );
            Message message2 = new(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKeyThree, MessageType.MT_DOCUMENT), 
                new MessageBody("message body3")
            );
            _mySqlOutbox.Add(messageEarliest, _context);
            Thread.Sleep(100);
            _mySqlOutbox.Add(message1, _context);
            Thread.Sleep(100);
            _mySqlOutbox.Add(message2, _context);

            // Not sure why (assuming time skew) but needs time to settle
            Thread.Sleep(7000);
        }

        [Fact]
        public void When_there_are_multiple_outstanding_messages_in_the_outbox_and_messages_within_an_interval_are_fetched()
        {
            var messages = _mySqlOutbox.OutstandingMessages(dispatchedSince: TimeSpan.Zero, _context);

            var msgs = messages as Message[] ?? messages.ToArray();
            Assert.True((msgs)?.Any());

            Assert.Equal(3, msgs?.Length);
        }
        
        [Fact]
        public async Task When_there_are_multiple_outstanding_messages_in_the_outbox_and_messages_within_an_interval_are_fetched_async()
        {
            var messages = await _mySqlOutbox.OutstandingMessagesAsync(dispatchedSince: TimeSpan.Zero, _context);

            var msgs = messages as Message[] ?? messages.ToArray();
            Assert.True((msgs)?.Any());
            Assert.Equal(3, msgs?.Length);
        }

        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
