using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.InMemory.Tests.Data;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Trait("Category", "InMemory")]
    [Trait("Fragile", "CI")]
    public class InboxEntryTimeToLiveTests
    {
        [Fact]
        public async Task When_expiring_a_cache_entry_no_longer_there()
        {
            //Arrange
            const string contextKey = "Inbox_Cache_Expiry_Tests";
            
            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                //set some aggressive outbox reclamation times for the test
                EntryTimeToLive = TimeSpan.FromSeconds(1),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var command = new SimpleCommand();            
            
            //Act
            await inbox.AddAsync(command, contextKey, null);
            
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            
            //Trigger a cache clean
            SimpleCommand foundCommand = null;
            try
            {
                foundCommand = inbox.Get<SimpleCommand>(command.Id, contextKey, null);
            }
            catch (Exception e) when (e is RequestNotFoundException<SimpleCommand> || e is TypeLoadException)
            {
                //early sweeper run means it doesn't exist already
            }

            await Task.Delay(500); //Give the sweep time to run
            
            var afterExpiryExists = await inbox.ExistsAsync<SimpleCommand>(command.Id, contextKey, null);
            
            //Assert
            Assert.NotNull(foundCommand);
            Assert.False(afterExpiryExists);
        }

        [Fact]
        public async Task When_expiring_some_but_not_all()
        {
            //Arrange
            const string contextKey = "Inbox_Cache_Expiry_Tests";
            
            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                //set some aggressive outbox reclamation times for the test
                EntryTimeToLive = TimeSpan.FromSeconds(1),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            //Act
            var earlyCommands = new[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};            
            foreach (var command in earlyCommands)
            {
                await inbox.AddAsync(command, contextKey, null);
            }
            
            //expire these and allow another expiration to run
            timeProvider.Advance(TimeSpan.FromSeconds(5));
            
            inbox.ClearExpiredMessages();
            
            await Task.Delay(500); //Give the sweep time to run

            //add live entries
            var lateCommands = new[] { new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
            foreach (var command in lateCommands) //This will trigger cleanup
            {
                await inbox.AddAsync(command, contextKey, null);
            }
            
            //Assert
            Assert.Equal(3, inbox.EntryCount);

        }
    }
}
