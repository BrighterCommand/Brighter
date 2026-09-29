using System;
using System.Threading;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    /// <summary>
    /// An <see cref="InMemoryOutbox"/> whose compaction, once armed, blocks while it holds the cleanup lock.
    /// Lets a test deterministically hold the lock that the expiry scan also needs.
    /// </summary>
    internal sealed class BlockingCompactionInMemoryOutbox(TimeProvider timeProvider) : InMemoryOutbox(timeProvider)
    {
        private volatile bool _armed;

        public ManualResetEventSlim CompactionEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseCompaction { get; } = new(false);

        public void ArmCompactionBlock() => _armed = true;

        protected override void Compact(int entriesToRemove)
        {
            if (_armed)
            {
                CompactionEntered.Set();
                ReleaseCompaction.Wait(TimeSpan.FromSeconds(10));
            }

            base.Compact(entriesToRemove);
        }
    }
}
