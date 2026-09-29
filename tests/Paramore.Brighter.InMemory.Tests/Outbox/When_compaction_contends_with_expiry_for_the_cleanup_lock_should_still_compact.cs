using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxCompactionLockContentionTests
    {
        [Fact]
        public async Task When_compaction_contends_with_expiry_for_the_cleanup_lock_should_still_compact()
        {
            //Arrange
            const int limit = 5;

            var timeProvider = new FakeTimeProvider();
            var outbox = new BlockingExpiryInMemoryOutbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            //Fill the outbox to its limit with dispatched (and so compactable) messages; the clock does not move, so no expiry scan runs
            var ids = new string[limit];
            for (int i = 0; i < limit; i++)
            {
                ids[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(ids[i]), context);
                outbox.MarkDispatched(ids[i], context);
            }

            //From now on the expiry scan holds the cleanup lock until we release it
            outbox.ArmExpiryBlock();
            timeProvider.Advance(TimeSpan.FromMilliseconds(100));

            try
            {
                //A read is due an expiry scan, which takes the cleanup lock
                outbox.Get(ids[0], context);
                Assert.True(outbox.ExpiryEntered.Wait(TimeSpan.FromSeconds(5)), "Expiry scan should have taken the cleanup lock");

                //Act - this add takes us over the limit, so is due a compaction
                outbox.Add(new MessageTestDataBuilder(), context);

                //Give the compaction the chance to meet the held lock
                await Task.Delay(500);
            }
            finally
            {
                outbox.ReleaseExpiry.Set();
            }

            //Poll for compaction to complete
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert - 6 entries, compacted by 5 - (5 * 0.5) = 3 once the lock is free
            Assert.Equal(3, outbox.EntryCount);
        }
    }
}
