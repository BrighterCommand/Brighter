using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxCompactionDispatchedOnlyTests
    {
        [Fact]
        public async Task When_compacting_only_dispatched_messages_in_outbox()
        {
            //Arrange
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

            //Add 2 undispatched messages first (oldest by WriteTime)
            for (int i = 0; i < 2; i++)
            {
                undispatchedIds[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(undispatchedIds[i]), context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
            }

            //Add 3 dispatched messages after (newest by WriteTime)
            for (int i = 0; i < 3; i++)
            {
                dispatchedIds[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(dispatchedIds[i]), context);
                outbox.MarkDispatched(dispatchedIds[i], context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
            }

            Assert.Equal(5, outbox.EntryCount);

            //Advance past compaction cooldown
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));

            //Act - add one more to trigger compaction
            outbox.Add(new MessageTestDataBuilder(), context);

            //Poll for compaction to complete
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert - undispatched messages must survive compaction
            for (int i = 0; i < 2; i++)
            {
                var undispatchedResult = await outbox.GetAsync(undispatchedIds[i], context);
                Assert.False(undispatchedResult.IsEmpty, $"Undispatched message {i} should NOT be removed by compaction");
            }

            //Oldest dispatched messages should have been removed instead
            var oldestDispatched = await outbox.GetAsync(dispatchedIds[0], context);
            Assert.True(oldestDispatched.IsEmpty, "Oldest dispatched message should be removed by compaction");
        }
    }
}
