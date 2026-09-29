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
