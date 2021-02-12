using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    public class When_expiring_message_in_outbox
    {
        [Fact]
        public void When_expiring_a_cache_entry_no_longer_there()
        {
            //Arrange
            var outbox = new InMemoryOutbox()
            {
                //set some aggressive outbox reclamation times for the test
                PostTimeToLive = TimeSpan.FromMilliseconds(50),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };
            
            var messageId = Guid.NewGuid();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            outbox.Add(messageToAdd);
            
            Task.Delay(200).Wait(); //give the entry to time to expire

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
                   PostTimeToLive = TimeSpan.FromMilliseconds(50),
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
