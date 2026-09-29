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
    public class OutboxCleanupFailureTests
    {
        [Fact]
        public async Task When_expiry_throws_in_the_same_cleanup_pass_as_a_compaction_should_still_compact()
        {
            //Arrange
            const int limit = 5;

            var timeProvider = new FakeTimeProvider();
            var outbox = new FailingSecondExpiryInMemoryOutbox(timeProvider)
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

            //From now on the first expiry scan blocks the cleanup worker, and the second throws
            outbox.Arm();

            try
            {
                //A read is due an expiry scan, which occupies the cleanup worker
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
                outbox.Get(ids[0], context);
                Assert.True(outbox.ExpiryEntered.Wait(TimeSpan.FromSeconds(5)), "Expiry scan should have occupied the cleanup worker");

                //Act - this add is due both another expiry scan (which will throw) and a compaction, queued together behind the first scan
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
                outbox.Add(new MessageTestDataBuilder(), context);
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

            //Assert - 6 entries, compacted by 5 - (5 * 0.5) = 3 despite the failed expiry scan
            Assert.Equal(3, outbox.EntryCount);
        }
    }
}
