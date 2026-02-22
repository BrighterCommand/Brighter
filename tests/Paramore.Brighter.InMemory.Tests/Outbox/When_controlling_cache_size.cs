using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxMaxSize
    {
        [Fact]
        public async Task When_max_size_is_exceeded_shrink()
        {
            //Arrange
            const int limit = 5;
            
            var timeProvider = new FakeTimeProvider(); 
            var outbox = new InMemoryOutbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();
            for(int i =1; i <= limit; i++)
                outbox.Add(new MessageTestDataBuilder(), context);

            //Act
            Assert.Equal(5, outbox.EntryCount);
            
            outbox.Add(new MessageTestDataBuilder(), context);

            //Poll for compaction to complete - can be slow in CI environments
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //should clear compaction percentage from the outbox, and then add  the  new one
            Assert.Equal(3, outbox.EntryCount);
        }

        [Fact]
        public async Task When_shrinking_evict_oldest_messages_first()
        {
            //Arrange
            const int limit = 5;
            
            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider) 
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                Tracer = new BrighterTracer(timeProvider)
            };

            var messageIds = new string[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var context = new RequestContext();
            for (int i = 0; i <= limit - 1; i++)
            {
                await outbox.AddAsync(new MessageTestDataBuilder().WithId(messageIds[i]), context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
            }

            //Act
            Assert.Equal(5, outbox.EntryCount);
            
            await outbox.AddAsync(new MessageTestDataBuilder(), context);

            //Poll for compaction to complete - can be slow in CI environments
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //should clear compaction percentage from the outbox, and then add  the  new one
            Assert.True((await outbox.GetAsync(messageIds[0], context)).IsEmpty);
            Assert.True((await outbox.GetAsync(messageIds[1], context)).IsEmpty);
            Assert.True((await outbox.GetAsync(messageIds[2], context)).IsEmpty);
            Assert.False((await outbox.GetAsync(messageIds[3], context)).IsEmpty);
            Assert.False(((await outbox.GetAsync(messageIds[4], context)).IsEmpty));
        }
    }
}
