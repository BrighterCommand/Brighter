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
            //Arrange
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

            //Add many messages — well beyond default 2048 limit
            for (int i = 0; i < messageCount; i++)
            {
                outbox.Add(new MessageTestDataBuilder(), context);
            }

            //Act — no compaction should have occurred
            await Task.Delay(200); //Give background tasks time to run (if any)

            //Assert — all messages still present (compaction disabled)
            Assert.Equal(messageCount, outbox.EntryCount);

            //Now verify expiry still works for dispatched messages
            var dispatchedId = Guid.NewGuid().ToString();
            outbox.Add(new MessageTestDataBuilder().WithId(dispatchedId), context);
            outbox.MarkDispatched(dispatchedId, context);

            //Advance past TTL and scan interval
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000));

            //Trigger expiry via an outbox operation
            await outbox.GetAsync(dispatchedId, context);

            await Task.Delay(500); //Give the background expiry sweep time to run

            //Assert — dispatched message expired, but all undispatched messages remain
            var dispatchedResult = await outbox.GetAsync(dispatchedId, context);
            Assert.True(dispatchedResult.IsEmpty, "Dispatched message should still be removed by expiry when EntryLimit is -1");
            Assert.Equal(messageCount, outbox.EntryCount);
        }
    }
}
