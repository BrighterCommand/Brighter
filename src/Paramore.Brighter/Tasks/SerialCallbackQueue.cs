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
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// Runs callbacks one at a time, in the order they were queued, on a single worker. A producer uses it to
    /// raise publish confirmations off the broker's thread without queuing one thread-pool work item per
    /// confirmation (#4560).
    /// </summary>
    /// <remarks>
    /// A callback must handle its own exceptions; one that escapes is swallowed so that the worker keeps
    /// draining. <see cref="TryWait"/> lets a producer's dispose wait for the queued callbacks to finish.
    /// </remarks>
    public sealed class SerialCallbackQueue
    {
        private readonly System.Threading.Channels.Channel<Func<Task>> _callbacks =
            System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
        private readonly InFlightCallbackTracker _inFlight = new();
        private int _workerStarted;

        /// <summary>
        /// Queues a callback to run after every callback queued before it.
        /// </summary>
        /// <param name="callback">The callback; it must not throw.</param>
        public void Enqueue(Func<Task> callback)
        {
            _inFlight.Begin();
            if (!_callbacks.Writer.TryWrite(callback))
            {
                _inFlight.End();
                return;
            }

            if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) == 0)
                _ = Task.Run(DrainAsync);
        }

        /// <summary>
        /// Waits for every queued callback to finish.
        /// </summary>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="stillInFlight">How many callbacks had not finished when the wait ended.</param>
        /// <returns><see langword="true"/> if every callback finished in time.</returns>
        public bool TryWait(TimeSpan timeout, out int stillInFlight) => _inFlight.TryWait(timeout, out stillInFlight);

        /// <summary>
        /// Stops accepting callbacks; the worker ends once it has run those already queued.
        /// </summary>
        public void Complete() => _callbacks.Writer.TryComplete();

        private async Task DrainAsync()
        {
            var reader = _callbacks.Reader;
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var callback))
                {
                    try
                    {
                        await callback().ConfigureAwait(false);
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
}
