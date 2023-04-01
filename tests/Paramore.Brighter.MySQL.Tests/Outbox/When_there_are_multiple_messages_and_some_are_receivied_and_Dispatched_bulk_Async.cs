using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxBulkAsyncTests : IDisposable
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly string _Topic1 = "test_topic";
        private readonly string _Topic2 = "test_topic3";
        private IEnumerable<Message> _messages;
        private readonly Message _message1;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly Message _message;
        private readonly MySqlOutboxSync _sqlOutbox;

        public MySqlOutboxBulkAsyncTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MySqlOutboxSync(_mySqlTestHelper.OutboxConfiguration);
            _message = new Message(new MessageHeader(Guid.NewGuid(), _Topic1, MessageType.MT_COMMAND),
                new MessageBody("message body"));
            _message1 = new Message(new MessageHeader(Guid.NewGuid(), _Topic2, MessageType.MT_EVENT),
                new MessageBody("message body2"));
            _message2 = new Message(new MessageHeader(Guid.NewGuid(), _Topic1, MessageType.MT_COMMAND),
                new MessageBody("message body3"));
            _message3 = new Message(new MessageHeader(Guid.NewGuid(), _Topic2, MessageType.MT_EVENT),
                new MessageBody("message body4"));
        }

        [Fact]
        public async Task When_there_are_multiple_messages_and_some_are_recievied_and_Dispatched_bulk_Async()
        {
            await _sqlOutbox.AddAsync(_message);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message1);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message2);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_message3);
            await Task.Delay(100);

            _messages = await _sqlOutbox.GetAsync(new[] { _message1.Id, _message2.Id });

            //should fetch 1 message
            _messages.Should().HaveCount(2);
            //should fetch expected message
            _messages.Should().Contain(m => m.Id == _message1.Id);
            _messages.Should().Contain(m => m.Id == _message2.Id);

            await _sqlOutbox.MarkDispatchedAsync(_messages.Select(m => m.Id), DateTime.UtcNow);

            await Task.Delay(TimeSpan.FromSeconds(5));

            var undispatchedMessages = await _sqlOutbox.OutstandingMessagesAsync(0);

            undispatchedMessages.Count().Should().Be(2);
        }

        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
