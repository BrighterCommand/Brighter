using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.Tests.Outbox.MsSql
{
    public class OutstandingMessagesTests 
    {
        private readonly Message _dispatchedMessage;
        private readonly MsSqlOutbox _sqlOutbox;
        private readonly MsSqlTestHelper _msSqlTestHelper;

        public OutstandingMessagesTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
            _dispatchedMessage = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _sqlOutbox.Add(_dispatchedMessage);

            //wait to create an oustanding period
            Task.Delay(1000).Wait();

        }
        
        [Fact]
        public void When_there_is_an_outstanding_message_in_the_outbox()
        {
            var outstandingMessage = _sqlOutbox.OutstandingMessages(500).SingleOrDefault();

            outstandingMessage.Should().NotBe(null);
            outstandingMessage.Id.Should().Be(_dispatchedMessage.Id);
        }
    }
}
