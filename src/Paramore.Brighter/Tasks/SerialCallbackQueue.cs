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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// Runs callbacks one at a time, in the order they were queued, on a single dedicated thread. A producer uses
    /// it to raise publish confirmations off the broker's thread without queuing any work on the thread pool, so a
    /// busy producer does not add to the pool's queue and confirmations keep flowing when the pool is starved (#4560).
    /// </summary>
    /// <remarks>
    /// Each callback runs inside a <see cref="BrighterAsyncContext"/>, so its continuations come back to the
    /// dedicated thread; only a continuation that opts out of the context (<c>ConfigureAwait(false)</c>) can land
    /// on the pool. A callback must handle its own exceptions; one that escapes is swallowed so that the thread
    /// keeps draining. <see cref="TryWait"/> lets a producer's dispose wait for the queued callbacks to finish.
    /// The thread starts with the first callback and ends after <see cref="Complete"/> once the queue is empty.
    /// </remarks>
    public sealed class SerialCallbackQueue
    {
        private readonly BlockingCollection<Func<Task>> _callbacks = new();
        private readonly InFlightCallbackTracker _inFlight = new();
        private int _threadStarted;

        /// <summary>
        /// Queues a callback to run after every callback queued before it.
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
        public void Complete() => _callbacks.CompleteAdding();

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
            foreach (var callback in _callbacks.GetConsumingEnumerable())
            {
                try
                {
                    BrighterAsyncContext.Run(callback);
                }
                catch
                {
                    // The callback owns its fault handling; swallow anything it let escape so later callbacks still run.
                }
                finally
                {
                    _inFlight.End();
                }
            }
        }
    }
}
