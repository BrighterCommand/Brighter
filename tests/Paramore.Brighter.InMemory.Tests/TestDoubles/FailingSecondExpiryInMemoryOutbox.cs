using System;
using System.Threading;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    /// <summary>
    /// An <see cref="InMemoryOutbox"/> whose first expiry scan, once armed, blocks while it holds the cleanup
    /// worker, and whose second expiry scan throws. Lets a test queue further cleanup requests behind the first
    /// scan, so that they are taken together in the pass where expiry fails.
    /// </summary>
    internal sealed class FailingSecondExpiryInMemoryOutbox(TimeProvider timeProvider) : InMemoryOutbox(timeProvider)
    {
        private volatile bool _armed;
        private int _armedScans;

        public ManualResetEventSlim ExpiryEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseExpiry { get; } = new(false);

        public void Arm() => _armed = true;

        protected override void RemoveExpiredMessages(DateTimeOffset now)
        {
            if (!_armed)
            {
                base.RemoveExpiredMessages(now);
                return;
            }

            if (Interlocked.Increment(ref _armedScans) == 1)
            {
                ExpiryEntered.Set();
                ReleaseExpiry.Wait(TimeSpan.FromSeconds(10));
                base.RemoveExpiredMessages(now);
                return;
            }

            throw new InvalidOperationException("Expiry scan failed");
        }
    }
}
