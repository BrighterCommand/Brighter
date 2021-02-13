using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.InMemory.Tests.Builders;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    public class OutboxMaxSize
    {
        [Fact]
        public void When_max_size_is_exceeded_shrink()
        {
            //Arrange
            const int limit = 5;
            
            var outbox = new InMemoryOutbox()
            {
                MessageLimit = limit,
                CompactionPercentage = 0.5
            };
            
            for(int i =1; i <= limit; i++)
                outbox.Add(new MessageBuilder());

            //Act
            outbox.MessageCount.Should().Be(5);
            
            outbox.Add(new MessageBuilder());

            Task.Delay(500).Wait(); //Allow time for compaction to run
            
            //should clear compaction percentage from the outbox, and then add  the  new one
            outbox.MessageCount.Should().Be(3);
        }

        [Fact]
        public void When_shrinking_evict_oldest_messages_first()
        {
            //Arrange
            const int limit = 5;
            
            var outbox = new InMemoryOutbox()
            {
                MessageLimit = limit,
                CompactionPercentage = 0.5
            };

            var messageIds = new Guid[] {Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()};

            for (int i = 0; i <= limit - 1; i++)
            {
                outbox.Add(new MessageBuilder().WithId(messageIds[i]));
                Task.Delay(1000);
            }

            //Act
            outbox.MessageCount.Should().Be(5);
            
            outbox.Add(new MessageBuilder());

            Task.Delay(500).Wait(); //Allow time for compaction to run
            
            //should clear compaction percentage from the outbox, and then add  the  new one
            outbox.Get(messageIds[0]).Should().BeNull();
            outbox.Get(messageIds[1]).Should().BeNull();
            outbox.Get(messageIds[2]).Should().BeNull();
            outbox.Get(messageIds[3]).Should().NotBeNull();
            outbox.Get(messageIds[4]).Should().NotBeNull();
        }
    }
}
