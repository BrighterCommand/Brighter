using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Trait("Category", "InMemory")]
    public class InboxExpiryByWriteTimeTests
    {
        [Fact]
        public async Task When_inbox_expiry_removes_by_write_time()
        {
            //Arrange
            const string contextKey = "Inbox_WriteTime_Expiry_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryTimeToLive = TimeSpan.FromMilliseconds(500),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            //Add old commands
            var oldCommand1 = new SimpleCommand();
            var oldCommand2 = new SimpleCommand();
            await inbox.AddAsync(oldCommand1, contextKey, null);
            await inbox.AddAsync(oldCommand2, contextKey, null);

            //Advance time past TTL
            timeProvider.Advance(TimeSpan.FromMilliseconds(600));

            //Add a recent command (after time has advanced, so its WriteTime is fresh)
            var recentCommand = new SimpleCommand();
            await inbox.AddAsync(recentCommand, contextKey, null);

            //Advance past scan interval to allow expiry to trigger
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));

            //Act — trigger expiry via an inbox operation
            await inbox.ExistsAsync<SimpleCommand>(recentCommand.Id, contextKey, null);

            //Poll until the background expiry sweep completes
            var retries = 0;
            while (await inbox.ExistsAsync<SimpleCommand>(oldCommand1.Id, contextKey, null) && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert — old commands expired by WriteTime, recent command remains
            var oldCommand1Exists = await inbox.ExistsAsync<SimpleCommand>(oldCommand1.Id, contextKey, null);
            var oldCommand2Exists = await inbox.ExistsAsync<SimpleCommand>(oldCommand2.Id, contextKey, null);
            var recentCommandExists = await inbox.ExistsAsync<SimpleCommand>(recentCommand.Id, contextKey, null);

            Assert.False(oldCommand1Exists, "Old command 1 should be expired based on WriteTime");
            Assert.False(oldCommand2Exists, "Old command 2 should be expired based on WriteTime");
            Assert.True(recentCommandExists, "Recent command should NOT be expired");
        }
    }
}
