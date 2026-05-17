#region Sources

// This class is based on Stephen Cleary's AsyncContext in https://github.com/StephenCleary/AsyncEx
// The original code is licensed under the MIT License (MIT) <a href="https://github.com/StephenCleary/AsyncEx/blob/master/LICENSE>AsyncEx license</a>
// Modifies the original approach in Brighter which only provided a synchronization context, not a scheduler, and thus would
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

namespace Paramore.Brighter.Tasks;

/// <summary>
/// <see cref="SynchronizationContext"/> that dispatches callbacks to a <see cref="BrighterAsyncContext"/>.
/// </summary>
/// <remarks>
/// Adopts a single-threaded apartment model. All work - messages and callbacks - is queued to a single
/// work queue. When a callback is signalled, it is queued next and picked up when the current message
/// completes or waits. Strict ordering of messages may be lost as there is no guarantee what order I/O
/// operations will complete - do not use if strict ordering is required. Only uses one thread, so
/// predictable performance, but the queue may grow under load.
/// <para>
/// Lifecycle: after the owning <see cref="BrighterAsyncContext"/> has finished draining,
/// stray async continuations may still <see cref="Post"/> callbacks back. Those are absorbed
/// silently (the queue is closed, so the callback is dropped). <see cref="Send"/> cannot be
/// absorbed the same way because the caller blocks on completion; a post-shutdown
/// <see cref="Send"/> throws <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public sealed class BrighterSynchronizationContext : SynchronizationContext
{
    /// <summary>
    /// The async context this synchronization context dispatches to.
    /// </summary>
    public BrighterAsyncContext AsyncContext { get; }

    /// <summary>
    /// The id of the parent task inside a <see cref="BrighterAsyncContext.Run(Action)"/> invocation.
    /// </summary>
    /// <remarks>Used for debugging - records which task created this synchronization context.</remarks>
    internal int ParentTaskId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterSynchronizationContext"/> class.
    /// </summary>
    /// <param name="asyncContext">The async context to dispatch to.</param>
    public BrighterSynchronizationContext(BrighterAsyncContext asyncContext)
    {
#if NET
        ArgumentNullException.ThrowIfNull(asyncContext);
#else
        if (asyncContext is null) throw new ArgumentNullException(nameof(asyncContext));
#endif
        AsyncContext = asyncContext;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The copy shares the original's <see cref="AsyncContext"/> reference, so callbacks
    /// posted or sent through the copy still route to the original pump. <see cref="Equals"/>
    /// and <see cref="GetHashCode"/> treat the copy and the original as equal.
    /// </remarks>
    public override SynchronizationContext CreateCopy() => new BrighterSynchronizationContext(AsyncContext);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is BrighterSynchronizationContext other && AsyncContext == other.AsyncContext;

    /// <inheritdoc />
    public override int GetHashCode() => AsyncContext.GetHashCode();

    /// <inheritdoc />
    public override void OperationCompleted()
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationContext: OperationCompleted on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
        Debug.IndentLevel = 0;
#endif

        AsyncContext.OperationCompleted();
    }

    /// <inheritdoc />
    public override void OperationStarted()
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationContext: OperationStarted on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
        Debug.IndentLevel = 0;
#endif

        AsyncContext.OperationStarted();
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback callback, object? state)
    {
#if NET
        ArgumentNullException.ThrowIfNull(callback);
#else
        if (callback is null) throw new ArgumentNullException(nameof(callback));
#endif

#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationContext: Post {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
        Debug.IndentLevel = 0;
#endif

        // Factory.Run schedules the callback via our scheduler, which enqueues it with
        // propagateExceptions=false. The outer Enqueue re-adds the same task with
        // propagateExceptions=true so the pump's drain loop rethrows any exception the
        // callback raised (matching ASP.NET SynchronizationContext semantics).
        //
        // This is deliberate: the task runs once (the second queue pull finds it already
        // completed and TryExecuteTask no-ops), but the second entry is what triggers
        // WaitAndUnwrapException. Queueing once with propagateExceptions=true would
        // require bypassing the scheduler, but TaskScheduler.TryExecuteTask rejects tasks
        // that were not associated with it via Start/StartNew, leaving such tasks
        // un-runnable.
        AsyncContext.Enqueue(AsyncContext.Factory.Run(() => callback(state)), propagateExceptions: true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Throws <see cref="ObjectDisposedException"/> instead of blocking when the pump has
    /// shut down. A caller may also observe a spurious <see cref="ObjectDisposedException"/>
    /// when another thread closes the pump between this call's successful enqueue and its
    /// completion wait: the callback was delivered and will be processed, but this caller
    /// sees the shutdown signal and aborts. Callers racing shutdown must be prepared for
    /// this exception.
    /// </remarks>
    public override void Send(SendOrPostCallback callback, object? state)
    {
#if NET
        ArgumentNullException.ThrowIfNull(callback);
#else
        if (callback is null) throw new ArgumentNullException(nameof(callback));
#endif

#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationContext: Send {callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationContext: Parent Task {ParentTaskId}");
        Debug.IndentLevel = 0;
#endif

        // Current thread already owns the context - execute inline to prevent self-deadlock.
        if (BrighterAsyncContext.Current == AsyncContext)
        {
            callback(state);
            return;
        }

        var task = AsyncContext.Factory.Run(() => callback(state));

        // Factory.Run calls Enqueue synchronously on this thread. If the queue has shut
        // down (either before this call, or during it because Enqueue's TryAdd failed),
        // the task will never be pulled from the queue and WaitAndUnwrapException would
        // block forever. Throw so the caller can react to the shutdown race.
        if (AsyncContext.IsShutDown && !task.IsCompleted)
        {
            throw new ObjectDisposedException(
                nameof(BrighterAsyncContext),
                "BrighterAsyncContext has shut down; Send cannot deliver the callback to its pump.");
        }

        task.WaitAndUnwrapException();
    }
}
