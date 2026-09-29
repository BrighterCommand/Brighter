using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Category("InMemory")]
    public class OutboxEntryLimitDisabledTests
    {
        [Test]
        public async Task When_entry_limit_is_minus_one_no_compaction()
        {
            // Use a low ExpirationScanInterval so the compaction cooldown doesn't mask the test.
            // With EntryLimit = -1 the guard returns immediately; without it, EnforceCapacityLimit
            // would compute upperSize = -1 and (count >= -1) is always true, so compaction would
            // fire and remove entries.
            const int messageCount = 100;

            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider)
            {
                EntryLimit = -1,
                CompactionPercentage = 0.5,
                EntryTimeToLive = TimeSpan.FromMilliseconds(500),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            for (int i = 0; i < messageCount; i++)
            {
                Message msg = new MessageTestDataBuilder();
                outbox.Add(msg, context);
                outbox.MarkDispatched(msg.Id, context);
            }

            timeProvider.Advance(TimeSpan.FromMilliseconds(200));

            outbox.Add(new MessageTestDataBuilder(), context);

            await Task.Delay(200);

            await Assert.That(outbox.EntryCount).IsEqualTo(messageCount + 1);

            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));

            await outbox.GetAsync(Guid.NewGuid().ToString(), context);

            var retries = 0;
            while (outbox.EntryCount > 1 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            await Assert.That(outbox.EntryCount).IsEqualTo(1);
        }
    }
}
