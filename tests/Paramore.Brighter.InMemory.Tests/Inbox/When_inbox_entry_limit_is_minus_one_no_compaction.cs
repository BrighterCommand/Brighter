using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Category("InMemory")]
    public class InboxEntryLimitDisabledTests
    {
        [Test]
        public async Task When_inbox_entry_limit_is_minus_one_no_compaction()
        {
            // With EntryLimit = -1 the guard in EnforceCapacityLimit returns immediately.
            // Without it, (count >= -1) is always true and compaction would fire, removing
            // entries. Low ExpirationScanInterval keeps the cooldown from masking the test.
            const int messageCount = 100;
            const string contextKey = "Inbox_NoCompaction_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryLimit = -1,
                CompactionPercentage = 0.5,
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            for (int i = 0; i < messageCount; i++)
            {
                await inbox.AddAsync(new SimpleCommand(), contextKey, null);
            }

            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            await inbox.AddAsync(new SimpleCommand(), contextKey, null);

            await Task.Delay(200);

            await Assert.That(inbox.EntryCount).IsEqualTo(messageCount + 1);
        }
    }
}
