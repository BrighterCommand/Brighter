#region Sources

// This class is based on Stephen Cleary's AyncContext in https://github.com/StephenCleary/AsyncEx
// The original code is licensed under the MIT License (MIT) <a href="https://github.com/StephenCleary/AsyncEx/blob/master/LICENSE>AyncEx license</a>
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
using System.Diagnostics;
using System.Threading;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// Provides a Tasks that processes work on a single thread.
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
        private readonly ExecutionContext? _executionContext;

        /// <summary>
        /// Gets the synchronization helper.
        /// </summary>
        public BrighterSynchronizationHelper SynchronizationHelper { get; }

        /// <summary>
        /// Gets or sets the timeout for send operations.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>
        /// The Id of the parent task in Run, if any. 
        /// </summary>
        /// <remarks>
        ///  Used for debugging, tells us which task created any SynchronizationContext
        /// </remarks>
        public int ParentTaskId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrighterSynchronizationContext"/> class.
        /// </summary>
        /// <param name="synchronizationHelper">The synchronization helper.</param>
        public BrighterSynchronizationContext(BrighterSynchronizationHelper synchronizationHelper)
        {
            SynchronizationHelper = synchronizationHelper;
            _executionContext = ExecutionContext.Capture();
        }

        /// <summary>
        /// Creates a copy of the synchronization context.
        /// </summary>
        /// <returns>A new <see cref="System.Threading.SynchronizationContext"/> object.</returns>
        public override SynchronizationContext CreateCopy()
        {
            return new BrighterSynchronizationContext(SynchronizationHelper);
        }
        
        ///inheritdoc /> 
        public override bool Equals(object? obj)
        {
            var other = obj as BrighterSynchronizationContext;
            if (other == null)
                return false;
            return (SynchronizationHelper == other.SynchronizationHelper);
        }
        
        ///inheritdoc /> 
        public override int GetHashCode()
        {
            return SynchronizationHelper.GetHashCode();
        }

        /// <summary>
        /// Notifies the context that an operation has completed.
        /// </summary>
        public override void OperationCompleted()
        {
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: OperationCompleted on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;

            
            SynchronizationHelper.OperationCompleted();
        }

        /// <summary>
        /// Notifies the context that an operation has started.
        /// </summary>
        public override void OperationStarted()
        {
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: OperationStarted on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;

            SynchronizationHelper.OperationStarted();
        }

        /// <summary>
        /// Dispatches an asynchronous message to the synchronization context.
        /// </summary>
        /// <param name="callback">The delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback callback, object? state)
        {
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: Post {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;
            
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var ctxt = ExecutionContext.Capture();
            bool queued = SynchronizationHelper.Enqueue(new ContextMessage(callback, state, ctxt), true);
            
            if (queued) return;
            
            //NOTE: if we got here, something went wrong, we should have been able to queue the message
            //mostly this seems to be a problem with the task we are running completing, but work is still being queued to the 
            //synchronization context. 
            // If the execution context can help, we might be able to redirect; if not just run immediately on this thread
            
            var contextCallback = new ContextCallback(callback);
            if (ctxt != null && ctxt  != _executionContext)
            {
                Debug.WriteLine(string.Empty);
                Debug.IndentLevel = 1;
                Debug.WriteLine($"BrighterSynchronizationContext: Post Failed to queue {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
                Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
                Debug.IndentLevel = 0;
                SynchronizationHelper.ExecuteOnContext(ctxt, contextCallback, state);
            }
            else
            {
                Debug.WriteLine(string.Empty);
                Debug.IndentLevel = 1;
                Debug.WriteLine($"BrighterSynchronizationContext: Post Failed to queue {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
                Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
                Debug.IndentLevel = 0;
                //just execute inline
                SynchronizationHelper.ExecuteImmediately(contextCallback, state); 
            }
            Debug.WriteLine(string.Empty);
            
        }

        /// <summary>
        /// Dispatches a synchronous message to the synchronization context.
        /// </summary>
        /// <param name="callback">The delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Send(SendOrPostCallback callback, object? state)
        {
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: Send {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;
            
            // current thread already owns the context, so just execute inline to prevent deadlocks
            if (BrighterSynchronizationHelper.Current == SynchronizationHelper)
            {
                callback(state);
            }
            else
            {
                var ctxt = ExecutionContext.Capture();
                var task = SynchronizationHelper.MakeTask(new ContextMessage(callback, state, ctxt));
                if (!task.Wait(Timeout)) // Timeout mechanism
                    throw new TimeoutException("BrighterSynchronizationContext: Send operation timed out.");
            }
        }
    }
}
