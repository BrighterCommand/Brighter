using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxExpiryDispatchedOnlyTests
    {
        [Fact]
        public async Task When_expiring_only_dispatched_messages_in_outbox()
        {
            //Arrange
            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider)
            {
                EntryTimeToLive = TimeSpan.FromMilliseconds(500),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            var dispatchedMessageId = Guid.NewGuid().ToString();
            var undispatchedMessageId = Guid.NewGuid().ToString();

            var dispatchedMessage = new Message(
                new MessageHeader(dispatchedMessageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("dispatched body"));

            var undispatchedMessage = new Message(
                new MessageHeader(undispatchedMessageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("undispatched body"));

            outbox.Add(dispatchedMessage, context);
            outbox.Add(undispatchedMessage, context);

            //Mark only one message as dispatched
            outbox.MarkDispatched(dispatchedMessageId, context);

            //Advance time past TTL and scan interval so expiry triggers
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));

            //Act - trigger expiry via a Get operation
            await outbox.GetAsync(dispatchedMessageId, context);

            //Poll until the background expiry sweep completes
            var retries = 0;
            while ((await outbox.GetAsync(dispatchedMessageId, context)).Header.MessageType != MessageType.MT_NONE && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert - dispatched message should be expired, undispatched should remain
            var dispatchedResult = await outbox.GetAsync(dispatchedMessageId, context);
            var undispatchedResult = await outbox.GetAsync(undispatchedMessageId, context);

            Assert.True(dispatchedResult.IsEmpty, "Dispatched message should be removed by expiry");
            Assert.False(undispatchedResult.IsEmpty, "Undispatched message should NOT be removed by expiry");
        }
    }
}
