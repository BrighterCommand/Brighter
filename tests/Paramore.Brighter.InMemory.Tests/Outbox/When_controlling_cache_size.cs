using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Category("InMemory")]
    public class OutboxMaxSize
    {
        [Test]
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
            var ids = new string[limit];
            for(int i = 0; i < limit; i++)
            {
                ids[i] = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(ids[i]), context);
                outbox.MarkDispatched(ids[i], context);
            }

            //Act
            await Assert.That(outbox.EntryCount).IsEqualTo(5);

            var triggerId = Guid.NewGuid().ToString();
            outbox.Add(new MessageTestDataBuilder().WithId(triggerId), context);
            outbox.MarkDispatched(triggerId, context);

            //Poll for compaction to complete - can be slow in CI environments
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //should clear compaction percentage from the outbox, and then add  the  new one
            await Assert.That(outbox.EntryCount).IsEqualTo(3);
        }

        [Test]
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
                outbox.MarkDispatched(messageIds[i], context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
            }

            //Act
            await Assert.That(outbox.EntryCount).IsEqualTo(5);

            var triggerId = Guid.NewGuid().ToString();
            await outbox.AddAsync(new MessageTestDataBuilder().WithId(triggerId), context);
            outbox.MarkDispatched(triggerId, context);

            //Poll for compaction to complete - can be slow in CI environments
            int retries = 0;
            while (outbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //should clear compaction percentage from the outbox, and then add  the  new one
            await Assert.That((await outbox.GetAsync(messageIds[0], context)).IsEmpty).IsTrue();
            await Assert.That((await outbox.GetAsync(messageIds[1], context)).IsEmpty).IsTrue();
            await Assert.That((await outbox.GetAsync(messageIds[2], context)).IsEmpty).IsTrue();
            await Assert.That((await outbox.GetAsync(messageIds[3], context)).IsEmpty).IsFalse();
            await Assert.That(((await outbox.GetAsync(messageIds[4], context)).IsEmpty)).IsFalse();
        }
    }
}
