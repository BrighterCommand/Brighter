using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class MsSqlOutboxBulkGetAsyncTests : IDisposable
    {
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly string _Topic1 = "test_topic";
        private readonly string _Topic2 = "test_topic3";
        private IEnumerable<Message> _messages;
        private readonly Message _message1;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly Message _message;
        private readonly MsSqlOutbox _sqlOutbox;

        public MsSqlOutboxBulkGetAsyncTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
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

            _messages = await _sqlOutbox.GetAsync(new[] {_message1.Id, _message2.Id});

            //should fetch 1 message
            _messages.Should().HaveCount(2);
            //should fetch expected message
            _messages.First().Header.Topic.Should().Be(_Topic2);
            //should not fetch null messages
            _messages.Should().NotBeNull();
            //Second Should be Message 2
            _messages.First(m => m.Id == _message2.Id).Body.Should().Be(_message2.Body);

            await _sqlOutbox.MarkDispatchedAsync(_messages.Select(m => m.Id), DateTime.UtcNow);

            var undispatchedMessages = await _sqlOutbox.OutstandingMessagesAsync(0);

            undispatchedMessages.Count().Should().Be(2);
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
