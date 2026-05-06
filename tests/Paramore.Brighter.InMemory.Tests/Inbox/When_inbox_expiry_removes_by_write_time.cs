using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Category("InMemory")]
    public class InboxExpiryByWriteTimeTests
    {
        [Test]
        public async Task When_inbox_expiry_removes_by_write_time()
        {
            const string contextKey = "Inbox_WriteTime_Expiry_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryTimeToLive = TimeSpan.FromMilliseconds(500),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            var oldCommand1 = new SimpleCommand();
            var oldCommand2 = new SimpleCommand();
            await inbox.AddAsync(oldCommand1, contextKey, null);
            await inbox.AddAsync(oldCommand2, contextKey, null);

            timeProvider.Advance(TimeSpan.FromMilliseconds(600));

            var recentCommand = new SimpleCommand();
            await inbox.AddAsync(recentCommand, contextKey, null);

            timeProvider.Advance(TimeSpan.FromMilliseconds(200));

            await inbox.ExistsAsync<SimpleCommand>(recentCommand.Id, contextKey, null);

            var retries = 0;
            while (await inbox.ExistsAsync<SimpleCommand>(oldCommand1.Id, contextKey, null) && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            var oldCommand1Exists = await inbox.ExistsAsync<SimpleCommand>(oldCommand1.Id, contextKey, null);
            var oldCommand2Exists = await inbox.ExistsAsync<SimpleCommand>(oldCommand2.Id, contextKey, null);
            var recentCommandExists = await inbox.ExistsAsync<SimpleCommand>(recentCommand.Id, contextKey, null);

            await Assert.That(oldCommand1Exists).IsFalse();
            await Assert.That(oldCommand2Exists).IsFalse();
            await Assert.That(recentCommandExists).IsTrue();
        }
    }
}
