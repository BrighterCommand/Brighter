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
    public class OutboxCompactionTargetSizeTests
    {
        [Fact]
        public async Task When_expiry_leaves_the_outbox_over_its_limit_before_a_waiting_compaction_should_compact_only_to_target()
        {
            //Arrange
            const int limit = 5;
            const int targetSize = 2; //limit * CompactionPercentage

            var timeProvider = new FakeTimeProvider();
            var outbox = new BlockingExpiryInMemoryOutbox(timeProvider)
            {
                //a high limit while we fill, so that filling does not itself trigger a compaction
                EntryLimit = 100,
                CompactionPercentage = 0.5,
                EntryTimeToLive = TimeSpan.FromSeconds(1),
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();

            //3 messages dispatched long enough ago to have expired
            for (int i = 0; i < 3; i++)
            {
                var id = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(id), context);
                outbox.MarkDispatched(id, context, timeProvider.GetUtcNow().AddSeconds(-2));
            }

            //7 messages dispatched just now, so still live
            var liveId = string.Empty;
            for (int i = 0; i < 7; i++)
            {
                liveId = Guid.NewGuid().ToString();
                outbox.Add(new MessageTestDataBuilder().WithId(liveId), context);
                outbox.MarkDispatched(liveId, context);
            }

            //10 entries, now well over the limit
            outbox.EntryLimit = limit;

            //From now on the expiry scan holds the cleanup worker until we release it
            outbox.ArmExpiryBlock();

            try
            {
                //A read is due an expiry scan, which occupies the cleanup worker
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
                outbox.Get(liveId, context);
                Assert.True(outbox.ExpiryEntered.Wait(TimeSpan.FromSeconds(5)), "Expiry scan should have occupied the cleanup worker");

                //Act - this add is due a compaction, sized at 10 - 2 = 8 entries, which must wait for the expiry scan
                outbox.Add(new MessageTestDataBuilder(), context);
            }
            finally
            {
                outbox.ReleaseExpiry.Set();
            }

            //Poll for expiry and compaction to complete, then allow time for any over-trimming to show up
            int retries = 0;
            while (outbox.EntryCount > targetSize && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            await Task.Delay(500);

            //Assert - expiry leaves 8 (7 live + the undispatched add), still over the limit, so compaction
            //removes 8 - 2 = 6 rather than the 8 it was sized at, stopping at the target size
            Assert.Equal(targetSize, outbox.EntryCount);
        }
    }
}
