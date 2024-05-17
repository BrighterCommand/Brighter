using System;
using System.Threading.Tasks;
using FluentAssertions;
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
                EntryTimeToLive = TimeSpan.FromMilliseconds(50),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var command = new SimpleCommand();            
            
            //Act
            await inbox.AddAsync(command, contextKey);
            
            timeProvider.Advance(TimeSpan.FromMilliseconds(500));
            
            //Trigger a cache clean
            SimpleCommand foundCommand = null;
            try
            {
                foundCommand = inbox.Get<SimpleCommand>(command.Id, contextKey);
            }
            catch (Exception e) when (e is RequestNotFoundException<SimpleCommand> || e is TypeLoadException)
            {
                //early sweeper run means it doesn't exist already
            }

            await Task.Delay(500); //Give the sweep time to run
            
            var afterExpiryExists = await inbox.ExistsAsync<SimpleCommand>(command.Id, contextKey);
            
            //Assert
            foundCommand.Should().NotBeNull();
            afterExpiryExists.Should().BeFalse();
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
                EntryTimeToLive = TimeSpan.FromMilliseconds(500),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var earlyCommands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};            
            
            //Act
            foreach (var command in earlyCommands)
            {
                await inbox.AddAsync(command, contextKey);
            }
            
            //expire these
            timeProvider.Advance(TimeSpan.FromMilliseconds(500));

            await Task.Delay(500); //Give the sweep time to run

            var lateCommands = new SimpleCommand[] { new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};

            foreach (var command in lateCommands) //This will trigger cleanup
            {
                await inbox.AddAsync(command, contextKey);
            }
            
            //Assert
            inbox.EntryCount.Should().Be(3);

        }
    }
}
