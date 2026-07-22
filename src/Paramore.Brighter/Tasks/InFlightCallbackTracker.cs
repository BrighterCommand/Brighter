#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// Counts callbacks a producer has started but not yet observed completing, so its dispose path
    /// can wait for them to drain. Used by producers whose confirmation callbacks run on worker
    /// tasks (they must not block a broker client's dispatch thread): the raise site calls
    /// <see cref="Begin"/> before scheduling the callback and <see cref="End"/> in its finally, and
    /// disposal calls <see cref="TryWait"/> after the broker's own acks have been drained.
    /// </summary>
    /// <remarks>
    /// The drain waiter is allocated lazily by <see cref="TryWait"/>, so the per-callback cost on
    /// the confirmation path is a single short lock, with no allocation. Public so that Brighter's
    /// transport packages can consume it across the assembly boundary; it is not intended as a
    /// general-purpose synchronization primitive.
    /// </remarks>
    public sealed class InFlightCallbackTracker
    {
        private readonly object _lock = new();
        private int _inFlight;
        private TaskCompletionSource<bool>? _drained;

        /// <summary>
        /// Records that a callback has been started. Every call must be balanced by exactly one
        /// <see cref="End"/> (call it in a finally), or <see cref="TryWait"/> will wait out its
        /// full timeout.
        /// </summary>
        public void Begin()
        {
            lock (_lock)
            {
                _inFlight++;
            }
        }

        /// <summary>
        /// Records that a callback has completed, releasing any drain waiter when it was the last
        /// one in flight.
        /// </summary>
        public void End()
        {
            lock (_lock)
            {
                // An unbalanced End would drive the count negative and the drain waiter would never
                // release; assert loudly in debug builds and refuse to go below zero in release.
                Debug.Assert(_inFlight > 0, "InFlightCallbackTracker.End called without a matching Begin");
                if (_inFlight > 0)
                    _inFlight--;

                if (_inFlight == 0 && _drained is not null)
                {
                    _drained.TrySetResult(true);
                    _drained = null;
                }
            }
        }

        /// <summary>
        /// Blocks until all in-flight callbacks have completed, or the timeout elapses.
        /// </summary>
        /// <remarks>
        /// A <see cref="Begin"/> that races in after the wait has observed zero and returned is not
        /// tracked by that wait — callers are expected to have stopped producing new callbacks
        /// (e.g. drained the broker's acks) before waiting, as a shutdown path does.
        /// </remarks>
        /// <param name="timeout">How long to wait for the drain.</param>
        /// <param name="stillInFlight">The number of callbacks still running when the wait ended.</param>
        /// <returns>True when the tracker drained within the timeout; false when callbacks remain.</returns>
        public bool TryWait(TimeSpan timeout, out int stillInFlight)
        {
            Task drainedTask;
            lock (_lock)
            {
                if (_inFlight == 0)
                {
                    stillInFlight = 0;
                    return true;
                }

                _drained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                drainedTask = _drained.Task;
            }

            if (drainedTask.Wait(timeout))
            {
                stillInFlight = 0;
                return true;
            }

            lock (_lock)
            {
                stillInFlight = _inFlight;
            }

            return stillInFlight == 0;
        }
    }
}
