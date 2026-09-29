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
    public class OutboxCompactionCurrentCountTests
    {
        [Fact]
        public async Task When_expiry_brings_the_outbox_under_its_limit_before_a_waiting_compaction_should_not_compact()
        {
            //Arrange
            const int limit = 5;

            var timeProvider = new FakeTimeProvider();
            var outbox = new BlockingExpiryInMemoryOutbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                EntryTimeToLive = TimeSpan.FromSeconds(1),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            //3 messages dispatched long enough ago to have expired
            var expiredIds = new string[3];
            for (int i = 0; i < expiredIds.Length; i++)
            {
                expiredIds[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(expiredIds[i]), context);
                outbox.MarkDispatched(expiredIds[i], context, timeProvider.GetUtcNow().AddSeconds(-2));
            }

            //2 messages dispatched just now, so still live
            var liveIds = new string[2];
            for (int i = 0; i < liveIds.Length; i++)
            {
                liveIds[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(liveIds[i]), context);
                outbox.MarkDispatched(liveIds[i], context);
            }

            //From now on the expiry scan holds the cleanup lock until we release it
            outbox.ArmExpiryBlock();

            try
            {
                //A read is due an expiry scan, which takes the cleanup lock
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
                outbox.Get(liveIds[0], context);
                Assert.True(outbox.ExpiryEntered.Wait(TimeSpan.FromSeconds(5)), "Expiry scan should have taken the cleanup lock");

                //Act - this add takes us to the limit, so is due a compaction, which must wait for the expiry scan
                outbox.Add(new MessageTestDataBuilder(), context);

                //Give the compaction the chance to meet the held lock
                await Task.Delay(500);
            }
            finally
            {
                outbox.ReleaseExpiry.Set();
            }

            //Poll for expiry to remove the expired messages, then allow time for the compaction to run
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            await Task.Delay(500);

            //Assert - expiry left 3 entries, under the limit, so compaction should not remove the live messages
            Assert.Equal(3, outbox.EntryCount);
            foreach (var liveId in liveIds)
            {
                Assert.False((await outbox.GetAsync(liveId, context)).IsEmpty, "Live dispatched message should not be compacted");
            }
        }
    }
}
