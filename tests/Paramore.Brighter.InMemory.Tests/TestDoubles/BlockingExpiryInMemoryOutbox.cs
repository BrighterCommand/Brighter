using System;
using System.Threading;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    /// <summary>
    /// An <see cref="InMemoryOutbox"/> whose expiry scan, once armed, blocks while it holds the cleanup lock.
    /// Lets a test deterministically hold the lock that compaction also needs.
    /// </summary>
    internal sealed class BlockingExpiryInMemoryOutbox(TimeProvider timeProvider) : InMemoryOutbox(timeProvider)
    {
        private volatile bool _armed;

        public ManualResetEventSlim ExpiryEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseExpiry { get; } = new(false);

        public void ArmExpiryBlock() => _armed = true;

        protected override void RemoveExpiredMessages(DateTimeOffset now)
        {
            if (_armed)
            {
                ExpiryEntered.Set();
                ReleaseExpiry.Wait(TimeSpan.FromSeconds(10));
            }

            base.RemoveExpiredMessages(now);
        }
    }
}
