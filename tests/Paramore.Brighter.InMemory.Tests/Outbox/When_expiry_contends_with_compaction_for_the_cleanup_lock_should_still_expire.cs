#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class OutboxExpiryLockContentionTests
    {
        [Fact]
        public async Task When_expiry_contends_with_compaction_for_the_cleanup_lock_should_still_expire()
        {
            //Arrange
            const int limit = 5;

            var timeProvider = new FakeTimeProvider();
            var outbox = new BlockingCompactionInMemoryOutbox(timeProvider)
            {
                EntryLimit = limit,
                CompactionPercentage = 0.5,
                EntryTimeToLive = TimeSpan.FromSeconds(1),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            //Fill the outbox to its limit with dispatched messages; the clock does not move, so no expiry scan runs
            for (int i = 0; i < limit; i++)
            {
                var id = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(id), context);
                outbox.MarkDispatched(id, context);
            }

            //From now on compaction holds the cleanup lock until we release it
            outbox.ArmCompactionBlock();

            //An undispatched message takes us over the limit; compaction will remove the 3 oldest dispatched messages
            var undispatchedId = Guid.NewGuid().ToString();
            outbox.Add(new MessageTestDataBuilder().WithId(undispatchedId), context);

            try
            {
                Assert.True(outbox.CompactionEntered.Wait(TimeSpan.FromSeconds(5)), "Compaction should have taken the cleanup lock");

                //Act - the remaining dispatched messages are now past their time to live, and a read is due an expiry scan
                timeProvider.Advance(TimeSpan.FromSeconds(2));
                outbox.Get(undispatchedId, context);

                //Give the expiry scan the chance to meet the held lock
                await Task.Delay(500);
            }
            finally
            {
                outbox.ReleaseCompaction.Set();
            }

            //Poll for compaction and expiry to complete
            int retries = 0;
            while (outbox.EntryCount > 1 && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            //Assert - compaction removes 3 dispatched, expiry removes the other 2; only the undispatched message remains
            Assert.Equal(1, outbox.EntryCount);
        }
    }
}
