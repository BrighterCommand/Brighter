#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// An item stored in an im-memory box needs to implement this, so we know if we should delete it from the box due to time or memory pressure 
    /// </summary>
    public interface IHaveABoxWriteTime
    {
        /// <summary>
        /// When was this item written to the box
        /// </summary>
        DateTimeOffset WriteTime { get; }
    }
     
    /// <summary>
    /// Base class for in-memory inboxes, handles TTL on entries and cache clearing requirements
    /// </summary>
    /// <typeparam name="T">An entry in the box, needs to a writetime so we know if we can clear it from the box</typeparam>
    public abstract class InMemoryBox<T>(TimeProvider timeProvider) where T: IHaveABoxWriteTime
    {
        protected readonly ConcurrentDictionary<string, T> Requests = new ConcurrentDictionary<string, T>();
        private DateTimeOffset _lastScanAt = timeProvider.GetUtcNow();
        private DateTimeOffset _lastCompactionAttemptAt = DateTimeOffset.MinValue;
        private readonly object _cleanupStateLock = new object();
        private DateTimeOffset? _expiryRequestedAt;
        private int? _compactionRequested;
        private bool _cleanupWorkerRunning;
        private int _entryLimit = 2048;

        /// <summary>
        /// How long an entry lives before it becomes eligible for expiry removal (defaults to 5 min).
        /// The reference time depends on the subclass: the outbox measures from dispatch time
        /// (<see cref="OutboxEntry.TimeFlushed"/>), while the inbox measures from write time.
        /// Think about your typical data volumes over a window of time, they all use memory to store.
        /// Memory is not reclaimed until an expiration scan.
        /// </summary>
        public TimeSpan EntryTimeToLive { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// if it has been this long since the last scan, any operation can trigger a scan of the
        /// cache to delete existing entries (defaults to 5 mins)
        /// Your expiration interval should greater than your time to live, and represents the frequency at which we will reclaim memory
        /// Note that scan check is triggered by an operation on the outbox, but it runs on a background thread to avoid latency with basic operation
        /// </summary>
        public TimeSpan ExpirationScanInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// For diagnostics 
        /// </summary>
        public int EntryCount => Requests.Count;
     
        /// <summary>
        /// How many messages should we retain, before we compact the Outbox.
        /// Use -1 to disable compaction. Must be -1 or a positive integer.
        /// </summary>
        public int EntryLimit
        {
            get => _entryLimit;
            set
            {
                if (value == 0 || value < -1)
                    throw new ArgumentOutOfRangeException(nameof(EntryLimit), value,
                        "EntryLimit must be -1 (disabled) or a positive integer.");
                _entryLimit = value;
            }
        }

        /// <summary>
        /// Target size as a fraction of <see cref="EntryLimit"/> after compaction.
        /// For example 0.5 means compact down to 50% of the limit. Defaults to 0.5.
        /// A value of 0 removes all eligible entries on each compaction.
        /// </summary>
        public double CompactionPercentage{ get; set; } = 0.5;

        public void ClearExpiredMessages()
        {
            var now = timeProvider.GetUtcNow();

            TimeSpan elapsedSinceLastScan = now - _lastScanAt;
            if (elapsedSinceLastScan < ExpirationScanInterval)
                return;

            _lastScanAt = now;

            //This is expensive, so hand it to the background cleanup worker
            lock (_cleanupStateLock)
            {
                _expiryRequestedAt = now;
            }

            EnsureCleanupWorker();
        }

        protected abstract void RemoveExpiredMessages(DateTimeOffset now);

        protected void EnforceCapacityLimit()
        {
                if (EntryLimit == -1)
                    return;

                var now = timeProvider.GetUtcNow();
                if ((now - _lastCompactionAttemptAt) < ExpirationScanInterval)
                    return;

                //Take a copy as it may change whilst we are doing the calculation, we ignore that
                var count = EntryCount;
                var upperSize = EntryLimit;

                if (count >= upperSize)
                {
                    int newSize = (int)(upperSize * CompactionPercentage);
                    int entriesToRemove = count - newSize;

                    _lastCompactionAttemptAt = now;

                    lock (_cleanupStateLock)
                    {
                        _compactionRequested = entriesToRemove;
                    }

                    EnsureCleanupWorker();
                }
        }

        protected abstract void Compact(int entriesToRemove);

        // Cleanup requests are coalesced onto a single background worker: a request made while the worker
        // is running is picked up by its next pass, so no request is lost (the scan interval and compaction
        // cooldown would stop it being retried) and no more than one pool thread is ever used for cleanup.
        // The worker runs on the default scheduler, so never on a caller's SynchronizationContext.
        private void EnsureCleanupWorker()
        {
            lock (_cleanupStateLock)
            {
                if (_cleanupWorkerRunning)
                    return;

                _cleanupWorkerRunning = true;
            }

            Task.Factory.StartNew(
                action: RunCleanupWorker,
                cancellationToken: CancellationToken.None,
                creationOptions: TaskCreationOptions.DenyChildAttach,
                scheduler: TaskScheduler.Default);
        }

        private void RunCleanupWorker()
        {
            try
            {
                while (TryTakeCleanupRequests(out var expiryRequestedAt, out var compactionRequested))
                {
                    if (expiryRequestedAt.HasValue)
                        RemoveExpiredMessages(expiryRequestedAt.Value);

                    if (compactionRequested.HasValue)
                        CompactIfStillOverLimit(compactionRequested.Value);
                }
            }
            catch
            {
                //let the next request start a new worker
                lock (_cleanupStateLock)
                {
                    _cleanupWorkerRunning = false;
                }

                throw;
            }
        }

        private bool TryTakeCleanupRequests(out DateTimeOffset? expiryRequestedAt, out int? compactionRequested)
        {
            lock (_cleanupStateLock)
            {
                expiryRequestedAt = _expiryRequestedAt;
                compactionRequested = _compactionRequested;
                _expiryRequestedAt = null;
                _compactionRequested = null;

                //deciding to stop under the same lock as a request is made means no request can be missed
                if (!expiryRequestedAt.HasValue && !compactionRequested.HasValue)
                    _cleanupWorkerRunning = false;

                return _cleanupWorkerRunning;
            }
        }

        // The request was sized when it was made; by the time it runs an expiry pass may already have
        // brought the box back under its limit, so re-check, and never compact below the target size
        private void CompactIfStillOverLimit(int entriesRequested)
        {
            var count = EntryCount;
            var upperSize = EntryLimit;

            if (upperSize == -1 || count < upperSize)
                return;

            int newSize = (int)(upperSize * CompactionPercentage);
            Compact(Math.Min(entriesRequested, count - newSize));
        }
    }
}
