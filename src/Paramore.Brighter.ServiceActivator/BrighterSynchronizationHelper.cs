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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator;

/// <summary>
/// The Brighter SynchronizationHelper holds the tasks that we need to execute as continuations of an async operation
/// and the scheduler that we will use to run those tasks. In this case we use a single-threaded scheduler to run the
/// continuations and not a thread pool scheduler. This is because we want to run the continuations on the same thread.
/// We also create a task factory that uses the scheduler, so that we can easily create tasks that are queued to the scheduler.
/// </summary>
public class BrighterSynchronizationHelper : IDisposable
{
    private readonly BrighterTaskQueue _taskQueue = new();
    private readonly ConcurrentDictionary<Task, byte> _activeTasks = new();
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly BrighterTaskScheduler _taskScheduler;
    private readonly TaskFactory _taskFactory;
    private int _outstandingOperations;
    private readonly TimeSpan _timeOut  = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterSynchronizationHelper"/> class.
    /// </summary>
    public BrighterSynchronizationHelper()
    {
        _taskScheduler = new BrighterTaskScheduler(this);
        _synchronizationContext = new BrighterSynchronizationContext(this);
        _taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.HideScheduler, TaskContinuationOptions.HideScheduler, _taskScheduler);
    }

    /// <summary>
    /// Access the task factory, intended for tests
    /// </summary>
    public TaskFactory Factory => _taskFactory;
    
    /// <summary>
    /// This is the same identifier as the context's <see cref="TaskScheduler"/>. Used for testing
    /// </summary>
    public int Id => _taskScheduler.Id;
    
    /// <summary>
    /// Access the task scheduler, intended for tests
    /// </summary>
    public TaskScheduler TaskScheduler => _taskScheduler;
    
    /// <summary>
    /// Access the synchoronization context, intended for tests
    /// </summary>
    public SynchronizationContext? SynchronizationContext => _synchronizationContext;

    /// <summary>
    /// Disposes the synchronization helper and clears the task queue.
    /// </summary>
    public void Dispose()
    {
        _taskQueue.CompleteAdding();
        _taskQueue.Dispose();
    }
 
    /// <summary>
    /// Gets the current synchronization helper.
    /// </summary>
    public static BrighterSynchronizationHelper? Current
    {
        get
        {
            var syncContext = SynchronizationContext.Current as BrighterSynchronizationContext;
            return syncContext?.SynchronizationHelper;
        }
    }

    /// <summary>
    /// Enqueues a context message for execution.
    /// </summary>
    /// <param name="message">The context message to enqueue.</param>
    /// <param name="propagateExceptions">Indicates whether to propagate exceptions.</param>
    public void Enqueue(ContextMessage message, bool propagateExceptions)
    {
        Enqueue(MakeTask(message), propagateExceptions);
    }

    /// <summary>
    /// Enqueues a task for execution.
    /// </summary>
    /// <param name="task">The task to enqueue.</param>
    /// <param name="propagateExceptions">Indicates whether to propagate exceptions.</param>
    public void Enqueue(Task task, bool propagateExceptions)
    {
        OperationStarted();
        task.ContinueWith(_ => OperationCompleted(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, _taskScheduler);
        if (_taskQueue.TryAdd(task, propagateExceptions))  _activeTasks.TryAdd(task, 0);
    }

    /// <summary>
    /// Creates a task from a context message.
    /// </summary>
    /// <param name="message">The context message.</param>
    /// <returns>The created task.</returns>
    public Task MakeTask(ContextMessage message)
    {
        return _taskFactory.StartNew(
            () => message.Callback(message.State),
            _taskFactory.CancellationToken, 
            _taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach, 
            _taskScheduler);
    }

    /// <summary>
    /// Notifies that an operation has completed.
    /// </summary>
    public void OperationCompleted()
    {
        var newCount = Interlocked.Decrement(ref _outstandingOperations);
        if (newCount == 0)
            _taskQueue.CompleteAdding();
    }

    /// <summary>
    /// Notifies that an operation has started.
    /// </summary>
    public void OperationStarted()
    {
        var newCount = Interlocked.Increment(ref _outstandingOperations);
    }

    /// <summary>
    /// Runs a void method and returns after all continuations have run.
    /// Propagates exceptions.
    /// </summary>
    /// <param name="action">The action to run.</param>
    public static void Run(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        using var synchronizationHelper = new BrighterSynchronizationHelper();

        var task = synchronizationHelper._taskFactory.StartNew(
            action,
            synchronizationHelper._taskFactory.CancellationToken,
            synchronizationHelper._taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            synchronizationHelper._taskFactory.Scheduler ?? TaskScheduler.Default
            );

        synchronizationHelper.Execute();
        task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs a method that returns a result and returns after all continuations have run.
    /// Propagates exceptions.
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <param name="func">The function to run.</param>
    /// <returns>The result of the function.</returns>
    public static TResult Run<TResult>(Func<TResult> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        using var synchronizationHelper = new BrighterSynchronizationHelper();

        var task = synchronizationHelper._taskFactory.StartNew(
            func,
            synchronizationHelper._taskFactory.CancellationToken,
            synchronizationHelper._taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            synchronizationHelper._taskFactory.Scheduler ?? TaskScheduler.Default
            );

        synchronizationHelper.Execute();
        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs an async void method and returns after all continuations have run.
    /// Propagates exceptions.
    /// </summary>
    /// <param name="func">The async function to run.</param>
    public static void Run(Func<Task> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        using var synchronizationHelper = new BrighterSynchronizationHelper();

        synchronizationHelper.OperationStarted();

        var task = synchronizationHelper._taskFactory.StartNew(
            func,
            synchronizationHelper._taskFactory.CancellationToken,
            synchronizationHelper._taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            synchronizationHelper._taskFactory.Scheduler ?? TaskScheduler.Default
            )
            .Unwrap()
            .ContinueWith(t =>
            {
                synchronizationHelper.OperationCompleted();
                t.GetAwaiter().GetResult();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, synchronizationHelper._taskScheduler);

        synchronizationHelper.Execute();
        task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Queues a task for execution and begins executing all tasks in the queue.
    /// Returns the result of the task proxy.
    /// Propagates exceptions.
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <param name="func">The async function to execute.</param>
    /// <returns>The result of the function.</returns>
    public static TResult Run<TResult>(Func<Task<TResult>> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        using var synchronizationHelper = new BrighterSynchronizationHelper();

        var task = synchronizationHelper._taskFactory.StartNew(
                func,
                synchronizationHelper._taskFactory.CancellationToken,
                synchronizationHelper._taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
                synchronizationHelper._taskFactory.Scheduler ?? TaskScheduler.Default
                )
                .Unwrap()
                .ContinueWith(t =>
                {
                    synchronizationHelper.OperationCompleted();
                    return t.GetAwaiter().GetResult();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, synchronizationHelper._taskScheduler);

        synchronizationHelper.Execute();
        return task.GetAwaiter().GetResult();
    }

    public void Execute()
    {
        BrighterSynchronizationContextScope.ApplyContext(_synchronizationContext, () =>
        {
            foreach (var (task, propagateExceptions) in _taskQueue.GetConsumingEnumerable())
            {
                var stopwatch = Stopwatch.StartNew();
                _taskScheduler.DoTryExecuteTask(task);
                stopwatch.Stop();

                if (stopwatch.Elapsed > _timeOut)
                    Debug.WriteLine(
                        $"Task execution took {stopwatch.ElapsedMilliseconds} ms, which exceeds the threshold.");

                if (!propagateExceptions) continue;

                task.GetAwaiter().GetResult();
                _activeTasks.TryRemove(task, out _);
            }
        });
    }

    public IEnumerable<Task> GetScheduledTasks()
    {
        return _taskQueue.GetScheduledTasks();
    }
}

/// <summary>
/// Represents a context message containing a callback and state.
/// </summary>
public struct ContextMessage
{
    public readonly SendOrPostCallback Callback;
    public readonly object? State;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMessage"/> struct.
    /// </summary>
    /// <param name="callback">The callback to execute.</param>
    /// <param name="state">The state to pass to the callback.</param>
    public ContextMessage(SendOrPostCallback callback, object? state)
    {
        Callback = callback;
        State = state;
    }
}
