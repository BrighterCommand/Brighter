#region Sources

// This class is based on Stephen Cleary's AyncContext in https://github.com/StephenCleary/AsyncEx
// The original code is licensed under the MIT License (MIT) <a href="https://github.com/StephenCleary/AsyncEx/blob/master/LICENSE>AyncEx license</a>
// Modifies the original approach in Brighter which only provided a synchronization synchronizationHelper, not a scheduler, and thus would
// not run continuations on the same thread as the async operation if used with ConfigureAwait(false).
// This is important for the ServiceActivator, as we want to ensure ordering on a single thread and not use the thread pool.

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
    public class BrighterSynchronizationContext : SynchronizationContext, IDisposable
    {
        private bool _disposed;
        
        /// <summary>
        /// Gets the synchronization helper.
        /// </summary>
        public BrighterAsyncContext AsyncContext { get; }

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
        /// <param name="asyncContext">The synchronization helper.</param>
        public BrighterSynchronizationContext(BrighterAsyncContext asyncContext)
        {
            AsyncContext = asyncContext;
        }
        
        public void Dispose()
        {
            //SynchronizationHelper.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Creates a copy of the synchronization context.
        /// </summary>
        /// <returns>A new <see cref="System.Threading.SynchronizationContext"/> object.</returns>
        public override SynchronizationContext CreateCopy()
        {
            return new BrighterSynchronizationContext(AsyncContext);
        }
        
        ///inheritdoc /> 
        public override bool Equals(object? obj)
        {
            var other = obj as BrighterSynchronizationContext;
            if (other == null)
                return false;
            return (AsyncContext == other.AsyncContext);
        }
        
        ///inheritdoc /> 
        public override int GetHashCode()
        {
            return AsyncContext.GetHashCode();
        }

        /// <summary>
        /// Notifies the context that an operation has completed.
        /// </summary>
        public override void OperationCompleted()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrighterSynchronizationContext));

#if DEBUG_CONTEXT
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: OperationCompleted on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;
            
#endif
            AsyncContext.OperationCompleted();
        }

        /// <summary>
        /// Notifies the context that an operation has started.
        /// </summary>
        public override void OperationStarted()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrighterSynchronizationContext));

#if DEBUG_CONTEXT
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: OperationStarted on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;
#endif

            AsyncContext.OperationStarted();
        }

        /// <summary>
        /// Dispatches an asynchronous message to the synchronization context.
        /// </summary>
        /// <param name="callback">The delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback callback, object? state)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrighterSynchronizationContext));
            
            if (callback == null) throw new ArgumentNullException(nameof(callback));

#if DEBUG_CONTEXT
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: Post {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;
#endif
            
            AsyncContext.Enqueue(AsyncContext.Factory.Run(() => callback(state)), true);
        }

        /// <summary>
        /// Dispatches a synchronous message to the synchronization context.
        /// </summary>
        /// <param name="callback">The delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Send(SendOrPostCallback callback, object? state)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrighterSynchronizationContext));

#if DEBUG_CONTEXT
            Debug.WriteLine(string.Empty);
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterSynchronizationContext: Send {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
            Debug.IndentLevel = 0;
#endif
            
            // current thread already owns the context, so just execute inline to prevent deadlocks
            if (BrighterAsyncContext.Current == AsyncContext)
            {
                callback(state);
            }
            else
            {
                var task = AsyncContext.Factory.Run(() => callback(state));
                task.WaitAndUnwrapException();
            }
        }
    }
}
