#region Sources

// This class is based on Stephen Cleary's AyncContext in https://github.com/StephenCleary/AsyncEx
// The original code is licensed under the MIT License (MIT) <a href="https://github.com/StephenCleary/AsyncEx/blob/master/LICENS>AyncEx license</a>
// Modifies the original approach in Brighter which only provided a synchronization synchronizationHelper, not a scheduler, and thus would
// not run continuations on the same thread as the async operation if used with ConfigureAwait(false).
// This is important for the ServiceActivator, as we want to ensure ordering on a single thread and not use the thread pool.

// Originally based on:

//Also based on:
// https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/
// https://raw.githubusercontent.com/Microsoft/vs-threading/refs/heads/main/src/Microsoft.VisualStudio.Threading/SingleThreadedSynchronizationContext.cs
// https://github.com/microsoft/referencesource/blob/master/System.Web/AspNetSynchronizationContext.cs

#endregion


using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Provides a SynchronizationContext that processes work on a single thread.
    /// </summary>
    /// <remarks>
    /// Adopts a single-threaded apartment model. We have one thread, all work - messages and callbacks are queued to a single work queue.
    /// When a callback is signaled, it is queued next and will be picked up when the current message completes or waits itself.
    /// Strict ordering of messages will be lost as there is no guarantee what order I/O operations will complete -
    /// do not use if strict ordering is required.
    /// Only uses one thread, so predictable performance, but may have many messages queued. Once queue length exceeds
    /// buffer size, we will stop reading new work.
    /// </remarks>
    internal class BrighterSynchronizationContext : SynchronizationContext
    {
        /// <summary>
        /// Gets the synchronization helper.
        /// </summary>
        public BrighterSynchronizationHelper SynchronizationHelper { get; }

        /// <summary>
        /// Gets or sets the timeout for send operations.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="BrighterSynchronizationContext"/> class.
        /// </summary>
        /// <param name="synchronizationHelper">The synchronization helper.</param>
        public BrighterSynchronizationContext(BrighterSynchronizationHelper synchronizationHelper)
        {
            SynchronizationHelper = synchronizationHelper;
        }

        /// <summary>
        /// Creates a copy of the synchronization context.
        /// </summary>
        /// <returns>A new <see cref="SynchronizationContext"/> object.</returns>
        public override SynchronizationContext CreateCopy()
        {
            return new BrighterSynchronizationContext(SynchronizationHelper);
        }

        /// <summary>
        /// Notifies the context that an operation has completed.
        /// </summary>
        public override void OperationCompleted()
        {
            SynchronizationHelper.OperationCompleted();
        }

        /// <summary>
        /// Notifies the context that an operation has started.
        /// </summary>
        public override void OperationStarted()
        {
            SynchronizationHelper.OperationStarted();
        }

        /// <summary>
        /// Dispatches an asynchronous message to the synchronization context.
        /// </summary>
        /// <param name="callback">The delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback callback, object? state)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (BrighterSynchronizationHelper.Current == SynchronizationHelper)
            {
                // Avoid reentrant calls causing deadlocks
                Task.Run(() => callback(state));
            }
            else
                SynchronizationHelper.Enqueue(new ContextMessage(callback, state), true);
        }

        /// <summary>
        /// Dispatches a synchronous message to the synchronization context.
        /// </summary>
        /// <param name="callback">The delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Send(SendOrPostCallback callback, object? state)
        {
            // current thread already owns the context, so just execute inline to prevent deadlocks
            if (BrighterSynchronizationHelper.Current == SynchronizationHelper)
            {
                callback(state);
            }
            else
            {
                var task = SynchronizationHelper.MakeTask(new ContextMessage(callback, state));
                if (!task.Wait(Timeout)) // Timeout mechanism
                    throw new TimeoutException("BrighterSynchronizationContext: Send operation timed out.");
            }
        }
    }
}
