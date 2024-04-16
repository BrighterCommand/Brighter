using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox
{
    public class SqliteOutboxBulkGetAsyncTests :IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly string _Topic1 = "test_topic";
        private readonly string _Topic2 = "test_topic3";
        private IEnumerable<Message> _messages;
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
 
            _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _Topic1, MessageType.MT_COMMAND),
                new MessageBody("message body"));
            _message1 = new Message(new MessageHeader(Guid.NewGuid().ToString(), _Topic2, MessageType.MT_EVENT),
                new MessageBody("message body2"));
            _message2 = new Message(new MessageHeader(Guid.NewGuid().ToString(), _Topic1, MessageType.MT_COMMAND),
                new MessageBody("message body3"));
            _message3 = new Message(new MessageHeader(Guid.NewGuid().ToString(), _Topic2, MessageType.MT_EVENT),
                new MessageBody("message body4"));
        }

        [Fact]
        public async Task When_there_are_multiple_messages_and_some_are_received_and_Dispatched_bulk_Async()
        {
            await _sqlOutbox.AddAsync(_message);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message1);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message2);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message3);
            await Task.Delay(100);

            await _sqlOutbox.MarkDispatchedAsync(new []{_message1.Id, _message2.Id}, DateTime.UtcNow);
            
            await Task.Delay(200);

            var undispatchedMessages = await _sqlOutbox.OutstandingMessagesAsync(0);

            undispatchedMessages.Count().Should().Be(2);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
