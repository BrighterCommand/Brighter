using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Category("InMemory")]
    public class OutboxExpiryDispatchedOnlyTests
    {
        [Test]
        public async Task When_expiring_only_dispatched_messages_in_outbox()
        {
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

            outbox.MarkDispatched(dispatchedMessageId, context);

            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));

            await outbox.GetAsync(dispatchedMessageId, context);

            var retries = 0;
            while ((await outbox.GetAsync(dispatchedMessageId, context)).Header.MessageType != MessageType.MT_NONE && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            var dispatchedResult = await outbox.GetAsync(dispatchedMessageId, context);
            var undispatchedResult = await outbox.GetAsync(undispatchedMessageId, context);

            await Assert.That(dispatchedResult.IsEmpty).IsTrue();
            await Assert.That(undispatchedResult.IsEmpty).IsFalse();
        }
    }
}
