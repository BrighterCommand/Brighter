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
            var outbox = new InMemoryOutbox(new BrighterTracer(), timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5
            };

            var context = new RequestContext();
            for(int i =1; i <= limit; i++)
                outbox.Add(new MessageTestDataBuilder(), context);

            //Act
            outbox.EntryCount.Should().Be(5);
            
            outbox.Add(new MessageTestDataBuilder(), context);

            await Task.Delay(500); //Allow time for compaction to run
            
            //should clear compaction percentage from the outbox, and then add  the  new one
            outbox.EntryCount.Should().Be(3);
        }

        [Fact]
        public async Task When_shrinking_evict_oldest_messages_first()
        {
            //Arrange
            const int limit = 5;
            
            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(new BrighterTracer(), timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5
            };

            var messageIds = new string[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var context = new RequestContext();
            for (int i = 0; i <= limit - 1; i++)
            {
                outbox.Add(new MessageTestDataBuilder().WithId(messageIds[i]), context);
                timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
            }

            //Act
            outbox.EntryCount.Should().Be(5);
            
            outbox.Add(new MessageTestDataBuilder(), context);

            await Task.Delay(500); //Allow time for compaction to run
            
            //should clear compaction percentage from the outbox, and then add  the  new one
            outbox.Get(messageIds[0], context).Should().BeNull();
            outbox.Get(messageIds[1], context).Should().BeNull();
            outbox.Get(messageIds[2], context).Should().BeNull();
            outbox.Get(messageIds[3], context).Should().NotBeNull();
            outbox.Get(messageIds[4], context).Should().NotBeNull();
        }
    }
}
