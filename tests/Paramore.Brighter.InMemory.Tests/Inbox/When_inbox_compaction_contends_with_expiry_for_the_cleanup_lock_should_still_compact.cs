using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Trait("Category", "InMemory")]
    public class InboxCompactionLockContentionTests
    {
        [Fact]
        public async Task When_inbox_compaction_contends_with_expiry_for_the_cleanup_lock_should_still_compact()
        {
            //Arrange
            const int limit = 5;
            const string contextKey = "Inbox_Compaction_Lock_Contention_Tests";

            var timeProvider = new FakeTimeProvider();
            var inbox = new BlockingExpiryInMemoryInbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100)
            };

            //Fill the inbox to its limit; the clock does not move, so no expiry scan runs
            var firstCommand = new SimpleCommand();
            inbox.Add(firstCommand, contextKey, null);

            for (int i = 1; i < limit; i++)
            {
                inbox.Add(new SimpleCommand(), contextKey, null);
            }

            //From now on the expiry scan holds the cleanup lock until we release it
            inbox.ArmExpiryBlock();
            timeProvider.Advance(TimeSpan.FromMilliseconds(100));

            try
            {
                //A read is due an expiry scan, which takes the cleanup lock
                inbox.Exists<SimpleCommand>(firstCommand.Id, contextKey, null);
                Assert.True(inbox.ExpiryEntered.Wait(TimeSpan.FromSeconds(5)), "Expiry scan should have taken the cleanup lock");

                //Act - this add takes us over the limit, so is due a compaction
                inbox.Add(new SimpleCommand(), contextKey, null);

                //Give the compaction the chance to meet the held lock
                await Task.Delay(500);
            }
            finally
            {
                inbox.ReleaseExpiry.Set();
            }

            //Poll for compaction to complete
            int retries = 0;
            while (inbox.EntryCount > 3 && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert - 6 entries, compacted by 5 - (5 * 0.5) = 3 once the lock is free
            Assert.Equal(3, inbox.EntryCount);
        }
    }
}
