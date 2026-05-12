using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Trait("Category", "InMemory")]
    public class InboxCompactionByWriteTimeTests
    {
        [Fact]
        public async Task When_inbox_compaction_removes_oldest_by_write_time()
        {
            //Arrange
            const int limit = 5;
            const string contextKey = "Inbox_Compaction_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5
            };

            var commandIds = new string[limit];

            //Add commands up to the limit, spacing them out in time
            for (int i = 0; i < limit; i++)
            {
                var command = new SimpleCommand();
                commandIds[i] = command.Id;
                await inbox.AddAsync(command, contextKey, null);
                timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
            }

            Assert.Equal(limit, inbox.EntryCount);

            //Act — add one more to trigger compaction
            var triggerCommand = new SimpleCommand();
            await inbox.AddAsync(triggerCommand, contextKey, null);

            //Poll for compaction to complete
            int retries = 0;
            while (inbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert — oldest commands (by WriteTime) should be removed, newest should remain
            var oldest0Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[0], contextKey, null);
            var oldest1Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[1], contextKey, null);
            var oldest2Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[2], contextKey, null);
            var newest3Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[3], contextKey, null);
            var newest4Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[4], contextKey, null);

            Assert.False(oldest0Exists, "Oldest command should be removed by compaction");
            Assert.False(oldest1Exists, "Second oldest command should be removed by compaction");
            Assert.False(oldest2Exists, "Third oldest command should be removed by compaction");
            Assert.True(newest3Exists, "Second newest command should survive compaction");
            Assert.True(newest4Exists, "Newest command should survive compaction");
        }
    }
}
