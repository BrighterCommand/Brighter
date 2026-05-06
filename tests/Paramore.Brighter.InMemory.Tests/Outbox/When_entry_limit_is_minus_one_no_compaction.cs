using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxEntryLimitDisabledTests
    {
        [Fact]
        public async Task When_entry_limit_is_minus_one_no_compaction()
        {
            //Arrange — use a low ExpirationScanInterval so the compaction cooldown doesn't
            //mask the test. With EntryLimit = -1 the guard returns immediately; without it,
            //EnforceCapacityLimit would compute upperSize = -1 and (count >= -1) is always
            //true, so compaction would fire and remove entries.
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

            //Add messages and mark them dispatched so they would be eligible for compaction
            for (int i = 0; i < messageCount; i++)
            {
                Message msg = new MessageTestDataBuilder();
                outbox.Add(msg, context);
                outbox.MarkDispatched(msg.Id, context);
            }

            //Advance past the compaction cooldown so EnforceCapacityLimit would proceed
            //if the EntryLimit != -1 guard were absent
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));

            //Act — trigger EnforceCapacityLimit via another Add
            outbox.Add(new MessageTestDataBuilder(), context);

            await Task.Delay(200); //Give background tasks time to run (if any)

            //Assert — all messages still present (compaction disabled by EntryLimit = -1)
            Assert.Equal(messageCount + 1, outbox.EntryCount);

            //Now verify expiry still works for dispatched messages
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));

            //Trigger expiry via an outbox operation
            await outbox.GetAsync(Guid.NewGuid().ToString(), context);

            //Poll for background expiry sweep to complete
            var retries = 0;
            while (outbox.EntryCount > 1 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert — dispatched messages expired, undispatched message remains
            Assert.Equal(1, outbox.EntryCount);
        }
    }
}
