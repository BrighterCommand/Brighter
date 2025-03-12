using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxBulkAsyncTests : IDisposable
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly RoutingKey _routingKeyOne = new RoutingKey("test_topic");
        private readonly RoutingKey _routingKeyTwo = new RoutingKey("test_topic3");
        private readonly Message _message1;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly Message _message;
        private readonly MySqlOutbox _sqlOutbox;
        private readonly RequestContext _context;

        public MySqlOutboxBulkAsyncTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _context = new RequestContext();

            _sqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);
            _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKeyOne, MessageType.MT_COMMAND),
                new MessageBody("message body"));
            _message1 = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKeyTwo, MessageType.MT_EVENT),
                new MessageBody("message body2"));
            _message2 = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKeyOne, MessageType.MT_COMMAND),
                new MessageBody("message body3"));
            _message3 = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKeyTwo, MessageType.MT_EVENT),
                new MessageBody("message body4"));
        }

        [Fact]
        public async Task When_there_are_multiple_messages_and_some_are_recievied_and_Dispatched_bulk_Async()
        {
            await _sqlOutbox.AddAsync(_message, _context);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message1, _context);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message2, _context);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message3, _context);
            await Task.Delay(100);

            await _sqlOutbox.MarkDispatchedAsync(new []{_message1.Id, _message2.Id}, _context, DateTime.UtcNow);

            await Task.Delay(TimeSpan.FromSeconds(5));

            var undispatchedMessages = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.Zero, _context);

            Assert.Equal(2, undispatchedMessages.Count());
        }

        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }

    }
}
