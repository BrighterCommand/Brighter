using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Trait("Category", "InMemory")]
    public class InboxEntryLimitDisabledTests
    {
        [Fact]
        public async Task When_inbox_entry_limit_is_minus_one_no_compaction()
        {
            //Arrange — with EntryLimit = -1 the guard in EnforceCapacityLimit returns
            //immediately. Without it, (count >= -1) is always true and compaction would
            //fire, removing entries. Use a low ExpirationScanInterval so the cooldown
            //doesn't mask the test.
            const int messageCount = 100;
            const string contextKey = "Inbox_NoCompaction_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryLimit = -1,
                CompactionPercentage = 0.5,
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            //Act — add many items
            for (int i = 0; i < messageCount; i++)
            {
                await inbox.AddAsync(new SimpleCommand(), contextKey, null);
            }

            //Advance past the compaction cooldown and trigger EnforceCapacityLimit via Add
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            await inbox.AddAsync(new SimpleCommand(), contextKey, null);

            await Task.Delay(200); //Give background tasks time to run (if any)

            //Assert — all items still present (compaction disabled by EntryLimit = -1)
            Assert.Equal(messageCount + 1, inbox.EntryCount);
        }
    }
}
