using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.InMemory.Tests.Data;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    public class InboxEntryTimeToLiveTests
    {
        [Fact]
        public void When_expiring_a_cache_entry_no_longer_there()
        {
            //Arrange
            const string contextKey = "Inbox_Cache_Expiry_Tests";
            
            var inbox = new InMemoryInbox()
            {
                //set some aggressive outbox reclamation times for the test
                EntryTimeToLive = TimeSpan.FromMilliseconds(50),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var command = new SimpleCommand();            
            
            //Act
            inbox.Add(command, contextKey);
            
            Task.Delay(500).Wait(); //give the entry to time to expire
            
            //Trigger a cache clean
            var foundCommand = inbox.Get<SimpleCommand>(command.Id, contextKey);

            Task.Delay(500).Wait(); //Give the sweep time to run
            
            var afterExpiryExists = inbox.Exists<SimpleCommand>(command.Id, contextKey);
            
            //Assert
            foundCommand.Should().NotBeNull();
            afterExpiryExists.Should().BeFalse();
        }

        [Fact]
        public void When_expiring_some_but_not_all()
        {
            //Arrange
            const string contextKey = "Inbox_Cache_Expiry_Tests";
            
            var inbox = new InMemoryInbox()
            {
                //set some aggressive outbox reclamation times for the test
                EntryTimeToLive = TimeSpan.FromMilliseconds(500),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var earlyCommands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};            
            
            //Act
            foreach (var command in earlyCommands)
            {
                inbox.Add(command, contextKey);
            }

            Task.Delay(1000).Wait();  //expire these items

            var lateCommands = new SimpleCommand[] { new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};

            foreach (var command in lateCommands) //This will trigger cleanup
            {
                inbox.Add(command, contextKey);
            }
            
            //Assert
            inbox.EntryCount.Should().Be(3);

        }
    }
}
