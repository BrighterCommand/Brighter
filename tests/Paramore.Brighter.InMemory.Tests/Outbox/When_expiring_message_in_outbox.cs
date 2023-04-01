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
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxEntryTimeToLiveTests
    {
        [Fact]
        public void When_expiring_a_cache_entry_no_longer_there()
        {
            //Arrange
            var outbox = new InMemoryOutbox()
            {
                //set some aggressive outbox reclamation times for the test
                EntryTimeToLive = TimeSpan.FromMilliseconds(50),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var messageId = Guid.NewGuid();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT),
                new MessageBody("message body"));


            //Act
            outbox.Add(messageToAdd);

            Task.Delay(500).Wait(); //give the entry to time to expire

            //Trigger a cache clean
            outbox.Get(messageId);

            Task.Delay(500).Wait(); //Give the sweep time to run

            var message = outbox.Get(messageId);

            //Assert
            message.Should().BeNull();
        }

        [Fact]
        public void When_over_ttl_but_no_sweep_run()
        {
               //Arrange
               var outbox = new InMemoryOutbox()
               {
                   //set low time to live but long sweep perioc
                   EntryTimeToLive = TimeSpan.FromMilliseconds(50),
                   ExpirationScanInterval = TimeSpan.FromMilliseconds(10000)
               };

               var messageId = Guid.NewGuid();
               var messageToAdd = new Message(
                   new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT),
                   new MessageBody("message body"));

               //Act
               outbox.Add(messageToAdd);

               Task.Delay(50).Wait(); //TTL has passed, but not expired yet

               var message = outbox.Get(messageId);

               //Assert
               message.Should().NotBeNull();
               message.Id.Should().Be(messageId);
        }
    }
}
