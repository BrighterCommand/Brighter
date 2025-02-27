using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class OutstandingMessagesTests 
    {
        private readonly Message _dispatchedMessage;
        private readonly MsSqlOutbox _sqlOutbox;

        public OutstandingMessagesTests()
        {
            MsSqlTestHelper msSqlTestHelper = new();
            msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(msSqlTestHelper.OutboxConfiguration);
            _dispatchedMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _sqlOutbox.Add(_dispatchedMessage, new RequestContext());

            //wait to create an outstanding period
            Task.Delay(1000).Wait();
        }
        
        [Fact]
        public void When_there_is_an_outstanding_message_in_the_outbox()
        {
            var outstandingMessage = _sqlOutbox.OutstandingMessages(TimeSpan.FromMilliseconds(100), new RequestContext()).SingleOrDefault();

            Assert.NotNull(outstandingMessage);
            Assert.Equal(_dispatchedMessage.Id, outstandingMessage?.Id);
        }
        
        [Fact]
        public async Task When_there_is_an_outstanding_message_in_the_outbox_async()
        {
            var outstandingMessage = (await _sqlOutbox.OutstandingMessagesAsync(TimeSpan.FromMilliseconds(100), new RequestContext())).SingleOrDefault();

            Assert.NotNull(outstandingMessage);
            Assert.Equal(_dispatchedMessage.Id, outstandingMessage?.Id);
        }
    }
}
