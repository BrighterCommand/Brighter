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
    public class OutboxExpiryCoalescingTests
    {
        [Fact]
        public async Task When_expiry_is_requested_repeatedly_during_a_cleanup_should_coalesce_into_one_scan()
        {
            //Arrange
            const int requestsWhileRunning = 5;

            var timeProvider = new FakeTimeProvider();
            var outbox = new BlockingExpiryInMemoryOutbox(timeProvider)
            {
                ExpirationScanInterval = TimeSpan.FromMilliseconds(100),
                Tracer = new BrighterTracer(timeProvider)
            };

            var context = new RequestContext();
            var id = Guid.NewGuid().ToString();
            outbox.Add(new MessageTestDataBuilder().WithId(id), context);

            //From now on the expiry scan holds the cleanup lock until we release it
            outbox.ArmExpiryBlock();

            try
            {
                //A read is due an expiry scan, which takes the cleanup lock
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
                outbox.Get(id, context);
                Assert.True(outbox.ExpiryEntered.Wait(TimeSpan.FromSeconds(5)), "Expiry scan should have taken the cleanup lock");

                //Act - every read is due another expiry scan while the first is still running
                for (int i = 0; i < requestsWhileRunning; i++)
                {
                    timeProvider.Advance(TimeSpan.FromMilliseconds(100));
                    outbox.Get(id, context);
                }
            }
            finally
            {
                outbox.ReleaseExpiry.Set();
            }

            //Poll for the follow-up scan, then allow time for any further scans to show up
            int retries = 0;
            while (outbox.ExpiryScans < 2 && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            await Task.Delay(500);

            //Assert - the running scan, plus one follow-up for all the requests made while it ran
            Assert.Equal(2, outbox.ExpiryScans);
        }
    }
}
