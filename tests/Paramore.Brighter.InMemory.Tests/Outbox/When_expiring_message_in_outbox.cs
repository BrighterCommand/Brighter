using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxEntryTimeToLiveTests
    {
        [Fact]
        public async Task When_expiring_a_cache_entry_no_longer_there()
        {
            //Arrange
            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider)
            {
                //set some aggressive outbox reclamation times for the test
                EntryTimeToLive = TimeSpan.FromMilliseconds(50),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };
            
            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            outbox.Add(messageToAdd, new RequestContext());
            
            timeProvider.Advance(TimeSpan.FromMilliseconds(500)); //give the entry to time to expire
            
            //Trigger a cache clean
            await outbox.GetAsync(messageId, new RequestContext());

            await Task.Delay(500); //Give the sweep time to run
            
            var message = await outbox.GetAsync(messageId, new RequestContext());
            
            //Assert
            Assert.True(message.Empty);
        }

        [Fact]
        public async Task When_over_ttl_but_no_sweep_run()
        {
               //Arrange
               var timeProvider = new FakeTimeProvider();
               var outbox = new InMemoryOutbox(timeProvider)
               {
                   //set low time to live but long sweep perioc
                   EntryTimeToLive = TimeSpan.FromMilliseconds(50),
                   ExpirationScanInterval = TimeSpan.FromMilliseconds(10000),
                   Tracer = new BrighterTracer(timeProvider)
               };
               
               var messageId = Guid.NewGuid().ToString();
               var messageToAdd = new Message(
                   new MessageHeader(messageId, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT), 
                   new MessageBody("message body"));
               
               
               //Act
               await outbox.AddAsync(messageToAdd, new RequestContext());
               
               timeProvider.Advance(TimeSpan.FromMilliseconds(50)); //TTL has passed, but not expired yet
   
               var message = await outbox.GetAsync(messageId, new RequestContext());
               
               //Assert
               Assert.NotNull(message);
               Assert.Equal(messageId, message.Id);
        }
    }
}
