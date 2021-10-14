using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class OutstandingMessagesTests 
    {
        private readonly Message _dispatchedMessage;
        private readonly MsSqlOutboxSync _sqlOutboxSync;
        private readonly MsSqlTestHelper _msSqlTestHelper;

        public OutstandingMessagesTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutboxSync = new MsSqlOutboxSync(_msSqlTestHelper.OutboxConfiguration);
            _dispatchedMessage = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _sqlOutboxSync.Add(_dispatchedMessage);

            //wait to create an oustanding period
            Task.Delay(1000).Wait();

        }
        
        [Fact]
        public void When_there_is_an_outstanding_message_in_the_outbox()
        {
            var outstandingMessage = _sqlOutboxSync.OutstandingMessages(100).SingleOrDefault();

            outstandingMessage.Should().NotBeNull();
            outstandingMessage.Id.Should().Be(_dispatchedMessage.Id);
        }
        
        [Fact]
        public async Task When_there_is_an_outstanding_message_in_the_outbox_async()
        {
            var outstandingMessage = (await _sqlOutboxSync.OutstandingMessagesAsync(100)).SingleOrDefault();

            outstandingMessage.Should().NotBeNull();
            outstandingMessage.Id.Should().Be(_dispatchedMessage.Id);
        }
    }
}
