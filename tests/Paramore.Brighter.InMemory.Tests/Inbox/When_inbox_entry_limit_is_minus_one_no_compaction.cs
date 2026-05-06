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
            //Arrange
            const int messageCount = 100;
            const string contextKey = "Inbox_NoCompaction_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryLimit = -1,
                CompactionPercentage = 0.5
            };

            //Act — add many items, well beyond the default 2048 limit
            for (int i = 0; i < messageCount; i++)
            {
                await inbox.AddAsync(new SimpleCommand(), contextKey, null);
            }

            await Task.Delay(200); //Give background tasks time to run (if any)

            //Assert — all items still present (compaction disabled)
            Assert.Equal(messageCount, inbox.EntryCount);
        }
    }
}
