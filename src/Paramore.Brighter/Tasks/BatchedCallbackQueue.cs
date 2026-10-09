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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// Runs callbacks in batches on a single dedicated thread. A producer uses it to raise publish confirmations off
    /// the broker's thread without queuing any work on the thread pool, so a busy producer does not add to the pool's
    /// queue and confirmations keep flowing when the pool is starved (#4560).
    /// </summary>
    /// <remarks>
    /// The thread takes up to 32 queued callbacks at a time, starts them in the order they were queued, and waits for
    /// all of them before taking more; so their I/O, such as marking a message dispatched in an outbox, overlaps,
    /// but callbacks may finish in any order. Each batch runs inside a <see cref="BrighterAsyncContext"/>, so
    /// continuations come back to the dedicated thread; only a continuation that opts out of the context (<c>ConfigureAwait(false)</c>) can land
    /// on the pool. A callback must handle its own exceptions; one that escapes is swallowed so that the thread
    /// keeps draining. <see cref="TryWait"/> lets a producer's dispose wait for the queued callbacks to finish.
    /// The thread starts with the first callback and ends after <see cref="Complete"/> once the queue is empty.
    /// </remarks>
    public sealed class BatchedCallbackQueue
    {
        private const int MaxBatchSize = 32;

        private readonly BlockingCollection<Func<Task>> _callbacks = new();
        private readonly InFlightCallbackTracker _inFlight = new();
        private int _threadStarted;
        private readonly long? _registryId;

        /// <summary>
        /// Creates a queue that the confirmation queue-depth metric does not report.
        /// </summary>
        public BatchedCallbackQueue() { }

        /// <summary>
        /// Creates a queue whose depth <see cref="PublishConfirmationMeter"/> reports under the producer's attributes.
        /// </summary>
        /// <param name="messagingSystem">The broker the owning producer publishes to.</param>
        /// <param name="destination">The topic the owning producer publishes to.</param>
        public BatchedCallbackQueue(MessagingSystem messagingSystem, RoutingKey destination)
        {
            MessagingSystem = messagingSystem;
            Destination = destination;
            _registryId = BatchedCallbackQueueRegistry.Register(this);
        }

        internal MessagingSystem? MessagingSystem { get; }

        internal RoutingKey? Destination { get; }

        /// <summary>
        /// How many callbacks are queued or running.
        /// </summary>
        internal int Depth => _inFlight.Count;

        /// <summary>
        /// Queues a callback; it starts after every callback queued before it has started.
        /// </summary>
        /// <param name="callback">The callback; it must not throw.</param>
        public void Enqueue(Func<Task> callback)
        {
            _inFlight.Begin();
            if (!TryAdd(callback))
            {
                _inFlight.End();
                return;
            }

            if (Interlocked.CompareExchange(ref _threadStarted, 1, 0) == 0)
                new Thread(Drain) { IsBackground = true, Name = "Brighter Confirmation Callbacks" }.Start();
        }

        /// <summary>
        /// Waits for every queued callback to finish.
        /// </summary>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="stillInFlight">How many callbacks had not finished when the wait ended.</param>
        /// <returns><see langword="true"/> if every callback finished in time.</returns>
        public bool TryWait(TimeSpan timeout, out int stillInFlight) => _inFlight.TryWait(timeout, out stillInFlight);

        /// <summary>
        /// Stops accepting callbacks; the thread ends once it has run those already queued.
        /// </summary>
        public void Complete()
        {
            _callbacks.CompleteAdding();
            if (Volatile.Read(ref _threadStarted) == 0)
                Unregister();
        }

        // A confirmation that arrives after Complete (a late broker ack during dispose) is dropped rather than
        // thrown back onto the broker's thread; BlockingCollection throws once adding is complete.
        private bool TryAdd(Func<Task> callback)
        {
            try
            {
                return _callbacks.TryAdd(callback);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void Drain()
        {
            foreach (var first in _callbacks.GetConsumingEnumerable())
            {
                var batch = TakeBatch(first);
                BrighterAsyncContext.Run(() => Task.WhenAll(batch.Select(RunAndRelease)));
            }

            Unregister();
        }

        private void Unregister()
        {
            if (_registryId is { } id)
                BatchedCallbackQueueRegistry.Unregister(id);
        }

        private List<Func<Task>> TakeBatch(Func<Task> first)
        {
            var batch = new List<Func<Task>> { first };
            while (batch.Count < MaxBatchSize && _callbacks.TryTake(out var next))
                batch.Add(next);
            return batch;
        }

        private async Task RunAndRelease(Func<Task> callback)
        {
            try
            {
                await callback();
            }
            catch
            {
                // The callback owns its fault handling; swallow anything it let escape so the rest still run.
            }
            finally
            {
                _inFlight.End();
            }
        }
    }
}
