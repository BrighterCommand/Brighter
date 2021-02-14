#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk> 

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion
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
                EntryLimit = limit,
                CompactionPercentage = 0.5
            };
            
            for(int i =1; i <= limit; i++)
                outbox.Add(new MessageBuilder());

            //Act
            outbox.EntryCount.Should().Be(5);
            
            outbox.Add(new MessageBuilder());

            Task.Delay(500).Wait(); //Allow time for compaction to run
            
            //should clear compaction percentage from the outbox, and then add  the  new one
            outbox.EntryCount.Should().Be(3);
        }

        [Fact]
        public void When_shrinking_evict_oldest_messages_first()
        {
            //Arrange
            const int limit = 5;
            
            var outbox = new InMemoryOutbox()
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5
            };

            var messageIds = new Guid[] {Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()};

            for (int i = 0; i <= limit - 1; i++)
            {
                outbox.Add(new MessageBuilder().WithId(messageIds[i]));
                Task.Delay(1000);
            }

            //Act
            outbox.EntryCount.Should().Be(5);
            
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
