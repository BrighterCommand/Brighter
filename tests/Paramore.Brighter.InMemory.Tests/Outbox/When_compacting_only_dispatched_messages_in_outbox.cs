using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Category("InMemory")]
    public class OutboxCompactionDispatchedOnlyTests
    {
        [Test]
        public async Task When_compacting_only_dispatched_messages_in_outbox()
        {
            const int limit = 5;

            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            var undispatchedIds = new string[2];
            var dispatchedIds = new string[3];

            for (int i = 0; i < 2; i++)
            {
                undispatchedIds[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(undispatchedIds[i]), context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
            }

            for (int i = 0; i < 3; i++)
            {
                dispatchedIds[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(dispatchedIds[i]), context);
                outbox.MarkDispatched(dispatchedIds[i], context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
            }

            await Assert.That(outbox.EntryCount).IsEqualTo(5);

            timeProvider.Advance(TimeSpan.FromMilliseconds(200));

            outbox.Add(new MessageTestDataBuilder(), context);

            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            for (int i = 0; i < 2; i++)
            {
                var undispatchedResult = await outbox.GetAsync(undispatchedIds[i], context);
                await Assert.That(undispatchedResult.IsEmpty).IsFalse();
            }

            var oldestDispatched = await outbox.GetAsync(dispatchedIds[0], context);
            await Assert.That(oldestDispatched.IsEmpty).IsTrue();
        }
    }
}
