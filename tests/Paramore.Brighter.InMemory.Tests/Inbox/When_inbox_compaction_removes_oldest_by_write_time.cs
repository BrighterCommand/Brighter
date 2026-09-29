using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Category("InMemory")]
    public class InboxCompactionByWriteTimeTests
    {
        [Test]
        public async Task When_inbox_compaction_removes_oldest_by_write_time()
        {
            const int limit = 5;
            const string contextKey = "Inbox_Compaction_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new InMemoryInbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5
            };

            var commandIds = new string[limit];

            for (int i = 0; i < limit; i++)
            {
                var command = new SimpleCommand();
                commandIds[i] = command.Id;
                await inbox.AddAsync(command, contextKey, null);
                timeProvider.Advance(TimeSpan.FromMilliseconds(1000));
            }

            await Assert.That(inbox.EntryCount).IsEqualTo(limit);

            var triggerCommand = new SimpleCommand();
            await inbox.AddAsync(triggerCommand, contextKey, null);

            int retries = 0;
            while (inbox.EntryCount > 3 && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            var oldest0Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[0], contextKey, null);
            var oldest1Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[1], contextKey, null);
            var oldest2Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[2], contextKey, null);
            var newest3Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[3], contextKey, null);
            var newest4Exists = await inbox.ExistsAsync<SimpleCommand>(commandIds[4], contextKey, null);

            await Assert.That(oldest0Exists).IsFalse();
            await Assert.That(oldest1Exists).IsFalse();
            await Assert.That(oldest2Exists).IsFalse();
            await Assert.That(newest3Exists).IsTrue();
            await Assert.That(newest4Exists).IsTrue();
        }
    }
}
