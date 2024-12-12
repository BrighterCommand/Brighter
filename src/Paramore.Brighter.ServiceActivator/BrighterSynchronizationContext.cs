

//Based on:
// https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/
// https://www.codeproject.com/Articles/5274751/Understanding-the-SynchronizationContext-in-NET-wi
// https://raw.githubusercontent.com/Microsoft/vs-threading/refs/heads/main/src/Microsoft.VisualStudio.Threading/SingleThreadedSynchronizationContext.cs

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Provides a SynchronizationContext that processes work on a single thread.
    /// </summary>
    /// <remarks>
    /// Adopts a single-threaded apartment model. We have one thread, all work - messages and callbacks are queued to a single work queue.
    /// When a callback is signaled, it is queued next and will be picked up when the current message completes or waits itself.
    /// Strict ordering of messages will be lost as there is no guarantee what order I/O operations will complete - do not use if strict ordering is required.
    /// Only uses one thread, so predictable performance, but may have many messages queued. Once queue length exceeds buffer size, we will stop reading new work.
    /// </remarks>
    public class BrighterSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<Message> _queue = new();
        private int _operationCount;
        private readonly int _ownedThreadId = Environment.CurrentManagedThreadId;

        /// <inheritdoc/>
        public override void OperationCompleted()
        {
            if (Interlocked.Decrement(ref _operationCount) == 0)
                Complete();
        }

        /// <inheritdoc/>
        public override void OperationStarted()
        {
            Interlocked.Increment(ref _operationCount);
        }

        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object? state)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            _queue.Add(new Message(d, state));
        }

        /// <inheritdoc/>
        public override void Send(SendOrPostCallback d, object? state)
        {
            if (_ownedThreadId == Environment.CurrentManagedThreadId)
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(ex);
                }
            }
            else
            {
                Exception? caughtException = null;
                var evt = new ManualResetEventSlim();
                try
                {
                    _queue.Add(new Message(s =>
                        {
                            try { d(state); }
                            catch (Exception ex) { caughtException = ex; }
                            finally { evt.Set(); }
                        },
                        state,
                        evt));

                    evt.Wait();

                    if (caughtException != null)
                    {
                        throw new TargetInvocationException(caughtException);
                    }
                }
                finally
                {
                    evt.Dispose();
                }
            }
        }

        /// <summary>
        /// Runs a loop to process all queued work items.
        /// </summary>
        public void RunOnCurrentThread()
        {
            foreach (var message in _queue.GetConsumingEnumerable())
            {
                message.Callback(message.State);
                message.FinishedEvent?.Set();
            }
        }

        /// <summary>
        /// Notifies the context that no more work will arrive.
        /// </summary>
        private void Complete()
        {
            _queue.CompleteAdding();
        }

        private struct Message(SendOrPostCallback callback, object? state, ManualResetEventSlim? finishedEvent = null)
        {
            public readonly SendOrPostCallback Callback = callback;
            public readonly object? State = state;
            public readonly ManualResetEventSlim? FinishedEvent = finishedEvent;
        }
    }
}
