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
    public class InMemoryBox<T>(TimeProvider timeProvider) where T: IHaveABoxWriteTime
    {
        protected readonly ConcurrentDictionary<string, T> Requests = new ConcurrentDictionary<string, T>();
        private DateTimeOffset _lastScanAt = timeProvider.GetUtcNow();
        private readonly object _cleanupRunningLockObject = new object();

        /// <summary>
        /// How long does an entry last in the Outbox before we delete it (defaults to 5 min)
        /// Think about your typical data volumes over a window of time, they all use memory to store
        /// But contrast with how long you want to be able to resend due to broker failure for.
        /// Memory is not reclaimed until an expiration scan
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
        /// How many messages should we retain, before we compact the Outbox
        /// </summary>
        public int EntryLimit { get; set; } = 2048;

        /// <summary>
        /// At what percentage of our size limit should we return, once we hit that limit
        /// </summary>
        public double CompactionPercentage{ get; set; }

        public void ClearExpiredMessages()
        {
            var now = timeProvider.GetUtcNow();

            TimeSpan elapsedSinceLastScan = now - _lastScanAt;
            if (elapsedSinceLastScan < ExpirationScanInterval)
                return;

            //This is expensive, so use a background thread
            Task.Factory.StartNew(
                action: state => RemoveExpiredMessages((DateTimeOffset)state!),
                state: now,
                cancellationToken: CancellationToken.None,
                creationOptions: TaskCreationOptions.DenyChildAttach,
                scheduler: TaskScheduler.Default);
            
            _lastScanAt = now;
        }

        private void RemoveExpiredMessages(DateTimeOffset now)
        {
            if (Monitor.TryEnter(_cleanupRunningLockObject))
            {
                try
                {
                    var expiredEntries =
                        Requests
                            .Where<KeyValuePair<string, T>>(entry => (now - entry.Value.WriteTime) >= EntryTimeToLive)
                            .Select(entry => entry.Key);

                    foreach (var key in expiredEntries)
                    {
                        //if this fails ignore, killed by something else like compaction
                        Requests.TryRemove(key, out _);
                    }

                }
                finally
                {
                    Monitor.Exit(_cleanupRunningLockObject);
                }
            }
        }

        protected void EnforceCapacityLimit()
        {
               //Take a copy as it may change whilst we are doing the calculation, we ignore that
                var count = EntryCount;
                var upperSize = EntryLimit;

                if (count >= upperSize)
                {
                    int newSize = (int)(count * CompactionPercentage);
                    int entriesToRemove = upperSize - newSize;

                    Task.Factory.StartNew(
                        action: state => Compact((int)state!),
                        state: entriesToRemove,
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                }
        }
        
        // Compaction algorithm is to sort into date deposited order, with oldest first
        // Then remove entries until newsize is reached
        private void Compact(int entriesToRemove)
        {
            if (Monitor.TryEnter(_cleanupRunningLockObject))
            {
                try
                {
                    var removalList =
                        Requests
                            .OrderBy(entry => entry.Value.WriteTime)
                            .Take(entriesToRemove)
                            .Select(entry => entry.Key);

                    foreach (var key in removalList)
                    {
                        //ignore errors, likely just something else has cleared it such as TTL eviction
                        Requests.TryRemove(key, out _);
                    }
                }
                finally
                {
                    Monitor.Exit(_cleanupRunningLockObject);
                }
            }
        }
    }
}
