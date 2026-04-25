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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// A single-threaded async context that pairs a <see cref="SynchronizationContext"/> with a
/// cooperating <see cref="TaskScheduler"/>, so continuations of async work run on the same
/// thread as their originator (not the thread pool).
/// </summary>
public sealed class BrighterAsyncContext : IDisposable
{
    private readonly BrighterTaskQueue _taskQueue = new();
    private readonly BrighterSynchronizationContext _synchronizationContext;
    private readonly BrighterTaskScheduler _taskScheduler;
    private readonly TaskFactory _taskFactory;

    private int _outstandingOperations;
    private int _executing;
    private int _shutdown;
    private int _disposed;

    /// <summary>
    /// The <see cref="TaskFactory"/> bound to this context's scheduler. Intended for tests
    /// and scheduler-aware consumers.
    /// </summary>
    public TaskFactory Factory => _taskFactory;

    /// <summary>
    /// The identifier of the context's <see cref="TaskScheduler"/>. Used for testing.
    /// </summary>
    public int Id => _taskScheduler.Id;

    /// <summary>
    /// Current outstanding operation count. Intended for debugging.
    /// </summary>
    public int OutstandingOperations => Volatile.Read(ref _outstandingOperations);

    /// <summary>
    /// The cooperating task scheduler. Intended for tests.
    /// </summary>
    public TaskScheduler TaskScheduler => _taskScheduler;

    /// <summary>
    /// The cooperating synchronization context. Intended for tests.
    /// </summary>
    public SynchronizationContext SynchronizationContext => _synchronizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterAsyncContext"/> class.
    /// </summary>
    public BrighterAsyncContext()
    {
        _taskScheduler = new BrighterTaskScheduler(this);
        _synchronizationContext = new BrighterSynchronizationContext(this);
        _taskFactory = new TaskFactory(
            CancellationToken.None,
            TaskCreationOptions.HideScheduler,
            TaskContinuationOptions.HideScheduler,
            _taskScheduler);
    }

    /// <summary>
    /// Returns <c>true</c> once the task queue has stopped accepting work (either because
    /// <see cref="Dispose"/> has been called, or because <see cref="OperationCompleted"/>
    /// has drained the last outstanding operation and completed the queue for adding).
    /// </summary>
    internal bool IsShutDown => Volatile.Read(ref _shutdown) != 0;

    /// <summary>
    /// Disposes the context, releasing the task queue. Idempotent.
    /// </summary>
    /// <remarks>
    /// The associated <see cref="BrighterSynchronizationContext"/> is intentionally not
    /// disposed here: late <see cref="SynchronizationContext.Post"/> calls from stray
    /// async continuations may still arrive after <c>Run</c> has returned. Those are
    /// absorbed silently. Late <see cref="SynchronizationContext.Send"/> calls cannot
    /// be absorbed (the caller waits for completion) and will throw
    /// <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Volatile.Write(ref _shutdown, 1);
        _taskQueue.Dispose();
    }

    /// <summary>
    /// Gets the current ambient <see cref="BrighterAsyncContext"/>, if any.
    /// </summary>
    public static BrighterAsyncContext? Current =>
        (SynchronizationContext.Current as BrighterSynchronizationContext)?.AsyncContext;

    /// <summary>
    /// Enqueues a task for execution by this context.
    /// </summary>
    /// <param name="task">The task to enqueue.</param>
    /// <param name="propagateExceptions">Whether to propagate exceptions back to <see cref="Execute"/>.</param>
    internal void Enqueue(Task task, bool propagateExceptions)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterAsyncContext: Enqueueing task {task.Id} on thread {Thread.CurrentThread.ManagedThreadId} for context {Id}");
        Debug.IndentLevel = 0;
#endif

        OperationStarted();

        if (!_taskQueue.TryAdd(task, propagateExceptions))
        {
            // Queue is already completed for adding: the task will never be pulled, so we
            // must not attach the completion continuation - if a caller later inline-executes
            // the stranded task via TryExecuteTaskInline, the continuation would fire and
            // double-decrement the outstanding-operations counter. Balance manually instead.
            // Record shutdown so Send can distinguish "pump will process this task" from
            // "task will never run".
            Volatile.Write(ref _shutdown, 1);
            OperationCompleted();
            return;
        }

        // Task is queued. Attach completion continuation to balance OperationStarted when
        // the task finishes. ExecuteSynchronously runs the continuation inline if the task
        // has already completed by the time we attach (e.g. the pump pulled and ran it
        // between TryAdd and here), so the continuation always fires exactly once.
        task.ContinueWith(
            static (_, state) => ((BrighterAsyncContext)state!).OperationCompleted(),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            _taskScheduler);
    }

    /// <summary>
    /// Notifies the context that an operation has completed.
    /// </summary>
    internal void OperationCompleted()
    {
        var newCount = Interlocked.Decrement(ref _outstandingOperations);

#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterAsyncContext: Operation completed on thread {Thread.CurrentThread.ManagedThreadId} for context {Id}");
        Debug.WriteLine($"BrighterAsyncContext: Outstanding operations: {newCount}");
        Debug.IndentLevel = 0;
#endif

        if (newCount == 0)
        {
            Volatile.Write(ref _shutdown, 1);
            _taskQueue.CompleteAdding();
        }
    }

    /// <summary>
    /// Notifies the context that an operation has started.
    /// </summary>
    internal void OperationStarted()
    {
        var newCount = Interlocked.Increment(ref _outstandingOperations);

#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterAsyncContext: Operation started on thread {Thread.CurrentThread.ManagedThreadId} for context {Id}");
        Debug.WriteLine($"BrighterAsyncContext: Outstanding operations: {newCount}");
        Debug.IndentLevel = 0;
#endif
    }

    /// <summary>
    /// Runs a synchronous action on this context and returns after all continuations
    /// have run. Propagates exceptions.
    /// </summary>
    public static void Run(Action action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(action);
#else
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif

        using var context = new BrighterAsyncContext();

        var task = context._taskFactory.Run(action);
        context.Execute(task);
        task.WaitAndUnwrapException();
    }

    /// <summary>
    /// Runs a synchronous function on this context and returns its result after all
    /// continuations have run. Propagates exceptions.
    /// </summary>
    public static TResult Run<TResult>(Func<TResult> func)
    {
#if NET
        ArgumentNullException.ThrowIfNull(func);
#else
        if (func is null) throw new ArgumentNullException(nameof(func));
#endif

        using var context = new BrighterAsyncContext();

        var task = context._taskFactory.Run(func);
        context.Execute(task);
        return task.WaitAndUnwrapException();
    }

    /// <summary>
    /// Runs an async delegate on this context and returns after all continuations have
    /// run. Propagates exceptions.
    /// </summary>
    public static void Run(Func<Task> func)
    {
#if NET
        ArgumentNullException.ThrowIfNull(func);
#else
        if (func is null) throw new ArgumentNullException(nameof(func));
#endif

        using var context = new BrighterAsyncContext();

        // Factory.Run(Func<Task>) returns an Unwrap proxy whose completion trails the
        // outer StartNew task's, so the outstanding-operations counter could reach zero
        // (and close the queue) while the async chain is still alive. Register an extra
        // outstanding operation and release it only when the proxy has actually finished.
        context.OperationStarted();
        var task = context._taskFactory.Run(func).ContinueWith(
            static (t, state) =>
            {
                try
                {
                    t.WaitAndUnwrapException();
                }
                finally
                {
                    ((BrighterAsyncContext)state!).OperationCompleted();
                }
            },
            context,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            context._taskScheduler);

        context.Execute(task);
        task.WaitAndUnwrapException();
    }

    /// <summary>
    /// Runs an async delegate on this context and returns its result after all continuations
    /// have run. Propagates exceptions.
    /// </summary>
    public static TResult Run<TResult>(Func<Task<TResult>> func)
    {
#if NET
        ArgumentNullException.ThrowIfNull(func);
#else
        if (func is null) throw new ArgumentNullException(nameof(func));
#endif

        using var context = new BrighterAsyncContext();

        // See Run(Func<Task>) above for why the outer OperationStarted/Completed pair
        // is required around the Unwrap proxy.
        context.OperationStarted();
        var task = context._taskFactory.Run(func).ContinueWith(
            static (t, state) =>
            {
                try
                {
                    return t.WaitAndUnwrapException();
                }
                finally
                {
                    ((BrighterAsyncContext)state!).OperationCompleted();
                }
            },
            context,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            context._taskScheduler);

        context.Execute(task);
        return task.WaitAndUnwrapException();
    }

    /// <summary>
    /// Executes the specified parent task plus every task in the queue until the queue drains.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Execute"/> is already running on another thread. The single-thread invariant
    /// forbids concurrent execution.
    /// </exception>
    public void Execute(Task parentTask)
    {
        if (Interlocked.CompareExchange(ref _executing, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"{nameof(BrighterAsyncContext)}.{nameof(Execute)} is not re-entrant and cannot be called concurrently on the same context.");
        }

#if DEBUG_CONTEXT
        Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterAsyncContext: Executing tasks on thread {Thread.CurrentThread.ManagedThreadId} for context {Id}");
        Debug.IndentLevel = 0;
#endif

        try
        {
            BrighterSynchronizationContextScope.ApplyContext(_synchronizationContext, parentTask, () =>
            {
                foreach (var (task, propagateExceptions) in _taskQueue.GetConsumingEnumerable())
                {
                    _taskScheduler.DoTryExecuteTask(task);

                    if (propagateExceptions)
                        task.WaitAndUnwrapException();
                }
            });
        }
        finally
        {
            Volatile.Write(ref _executing, 0);
        }

#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterAsyncContext: Execution completed on thread {Thread.CurrentThread.ManagedThreadId} for context {Id}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
#endif
    }

    /// <summary>
    /// The tasks currently scheduled in this context.
    /// </summary>
    internal IEnumerable<Task> GetScheduledTasks() => _taskQueue.GetScheduledTasks();
}
