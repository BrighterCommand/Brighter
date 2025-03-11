using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox
{
    public class SqliteOutboxBulkGetAsyncTests :IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly RoutingKey _routingKeyOne = new("test_topic");
        private readonly RoutingKey _routingKeyTwo = new("test_topic3");
        private readonly Message _message1;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly Message _message;
        private readonly SqliteOutbox _sqlOutbox;

        public SqliteOutboxBulkGetAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sqlOutbox = new SqliteOutbox(_sqliteTestHelper.OutboxConfiguration);
 
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
        public async Task When_there_are_multiple_messages_and_some_are_received_and_Dispatched_bulk_Async()
        {
            var context = new RequestContext();
            await _sqlOutbox.AddAsync(_message, context);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message1, context);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message2, context);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message3, context);
            await Task.Delay(100);

            await _sqlOutbox.MarkDispatchedAsync(new []{_message1.Id, _message2.Id}, context, DateTime.UtcNow);
            
            await Task.Delay(200);

            var undispatchedMessages = await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context);

            Assert.Equal(2, undispatchedMessages.Count());
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
