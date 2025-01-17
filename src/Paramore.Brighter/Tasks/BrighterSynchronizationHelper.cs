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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

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

    private readonly BrighterSynchronizationContext? _synchronizationContext;
    private readonly BrighterTaskScheduler _taskScheduler;
    private readonly TaskFactory _taskFactory;
    private readonly TaskFactory _defaultTaskFactory;

    private int _outstandingOperations;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterSynchronizationHelper"/> class.
    /// </summary>
    public BrighterSynchronizationHelper()
    {
        _taskScheduler = new BrighterTaskScheduler(this);
        _synchronizationContext = new BrighterSynchronizationContext(this);
        _taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.HideScheduler, TaskContinuationOptions.HideScheduler, _taskScheduler);

        _defaultTaskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    /// What tasks are currently active?
    /// <remarks>
    /// Used for debugging
    /// </remarks>
    /// </summary>
    public IEnumerable<Task> ActiveTasks => _activeTasks.Keys;

    /// <summary>
    /// A default task factory, used to return to the default task factory
    /// </summary>
    internal TaskFactory DefaultTaskFactory => _defaultTaskFactory;

    /// <summary>
    /// Access the task factory
    /// </summary>
    /// <remarks>
    ///  Intended for tests
    /// </remarks>
    public TaskFactory Factory => _taskFactory;

    /// <summary>
    /// This is the same identifier as the context's <see cref="TaskScheduler"/>. Used for testing
    /// </summary>
    public int Id => _taskScheduler.Id;

    /// <summary>
    /// How many operations are currently outstanding?
    /// </summary>
    /// <remarks>
    ///  Intended for debugging
    /// </remarks>
    public int OutstandingOperations { get; set; }

    /// <summary>
    /// Access the task scheduler,
    /// </summary>
    /// <remarks>
    ///  Intended for tests
    /// </remarks>
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
    public bool Enqueue(ContextMessage message, bool propagateExceptions)
    {
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Enqueueing message {message.Callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;

        return Enqueue(MakeTask(message), propagateExceptions);
    }

    /// <summary>
    /// Enqueues a task for execution.
    /// </summary>
    /// <param name="task">The task to enqueue.</param>
    /// <param name="propagateExceptions">Indicates whether to propagate exceptions.</param>
    public bool Enqueue(Task task, bool propagateExceptions)
    {
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Enqueueing task {task.Id} on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;

        OperationStarted();
        task.ContinueWith(_ => OperationCompleted(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, _taskScheduler);
        if (_taskQueue.TryAdd(task, propagateExceptions))
        {
            _activeTasks.TryAdd(task, 0);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a task from a context message.
    /// </summary>
    /// <param name="message">The context message.</param>
    /// <returns>The created task.</returns>
    public Task MakeTask(ContextMessage message)
    {
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper:Making task for message {message.Callback.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;

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
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Operation completed on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");

        var newCount = Interlocked.Decrement(ref _outstandingOperations);
        Debug.WriteLine($"BrighterSynchronizationHelper: Outstanding operations: {newCount}");
        Debug.IndentLevel = 0;

        if (newCount == 0)
            _taskQueue.CompleteAdding();
    }

    /// <summary>
    /// Notifies that an operation has started.
    /// </summary>
    public void OperationStarted()
    {
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Operation started on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");

        var newCount = Interlocked.Increment(ref _outstandingOperations);
        Debug.WriteLine($"BrighterSynchronizationHelper: Outstanding operations: {newCount}");
        Debug.IndentLevel = 0;
    }

    /// <summary>
    /// Runs a void method and returns after all continuations have run.
    /// Propagates exceptions.
    /// </summary>
    /// <param name="action">The action to run.</param>
    public static void Run(Action action)
    {
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("....................................................................................................................");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Running action {action.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;

        if (action == null)
            throw new ArgumentNullException(nameof(action));

        using var synchronizationHelper = new BrighterSynchronizationHelper();

        var task = synchronizationHelper._taskFactory.StartNew(
            action,
            synchronizationHelper._taskFactory.CancellationToken,
            synchronizationHelper._taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            synchronizationHelper._taskFactory.Scheduler ?? TaskScheduler.Default
            );

        synchronizationHelper.Execute(task);
        task.GetAwaiter().GetResult();

        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Action {action.Method.Name} completed on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine(synchronizationHelper.ActiveTasks.Count());
        Debug.IndentLevel = 0;
        Debug.WriteLine("....................................................................................................................");
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
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("....................................................................................................................");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Running function {func.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;

        if (func == null)
            throw new ArgumentNullException(nameof(func));

        using var synchronizationHelper = new BrighterSynchronizationHelper();

        var task = synchronizationHelper._taskFactory.StartNew(
            func,
            synchronizationHelper._taskFactory.CancellationToken,
            synchronizationHelper._taskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            synchronizationHelper._taskFactory.Scheduler ?? TaskScheduler.Default
            );

        synchronizationHelper.Execute(task);

        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Function {func.Method.Name} completed on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Active task count: {synchronizationHelper.ActiveTasks.Count()}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Task Status: {task.Status}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("....................................................................................................................");

        return task.GetAwaiter().GetResult();

    }

    /// <summary>
    /// Runs an async void method and returns after all continuations have run.
    /// Propagates exceptions.
    /// </summary>
    /// <param name="func">The async function to run.</param>
    public static void Run(Func<Task> func)
    {
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("....................................................................................................................");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Running function {func.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;

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

        synchronizationHelper.Execute(task);
        task.GetAwaiter().GetResult();

        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Function {func.Method.Name} completed on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Active task count: {synchronizationHelper.ActiveTasks.Count()}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Task Status: {task.Status}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("....................................................................................................................");
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
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("....................................................................................................................");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Running function {func.Method.Name} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;

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

        synchronizationHelper.Execute(task);

        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Function {func.Method.Name} completed on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Active task count: {synchronizationHelper.ActiveTasks.Count()}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Task Status: {task.Status}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("....................................................................................................................");

        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes the specified parent task and all tasks in the queue.
    /// </summary>
    /// <param name="parentTask">The parent task to execute.</param>
    public void Execute(Task parentTask)
    {
        Debug.WriteLine(string.Empty);
        Debug.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Executing tasks on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;

        BrighterSynchronizationContextScope.ApplyContext(_synchronizationContext, parentTask, () =>
        {

            foreach (var (task, propagateExceptions) in _taskQueue.GetConsumingEnumerable())
            {
                _taskScheduler.DoTryExecuteTask(task);

                if (!propagateExceptions) continue;

                task.GetAwaiter().GetResult();
                _activeTasks.TryRemove(task, out _);
            }
        });

        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Execution completed on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
    }

    /// <summary>
    /// Executes a task immediately on the current thread.
    /// </summary>
    /// <param name="callback">The task to execute.</param>
    /// <param name="state">The state object to pass to the task.</param>
    public void ExecuteImmediately(ContextCallback callback, object? state)
    {
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Executing task immediately on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.WriteLine($"BrighterSynchronizationHelper: Task {callback.Method.Name}");
        Debug.IndentLevel = 0;

        try
        {
           callback.Invoke(state);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"BrighterSynchronizationHelper: Execution errored on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id} with exception {e.Message}");
        }

        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Execution completed on thread {Thread.CurrentThread.ManagedThreadId} for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++");

    }

    /// <summary>
    /// Executes a task on the specified execution context.
    /// </summary>
    /// <param name="ctxt">The execution context.</param>
    /// <param name="contextCallback">The context callback to execute.</param>
    /// <param name="state">The state object to pass to the callback.</param>
    public void ExecuteOnContext(ExecutionContext ctxt, ContextCallback contextCallback, object? state)
    {
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Executing task immediately on original execution context for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;
        
        ExecutionContext.Run(ctxt, contextCallback, state);
        
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterSynchronizationHelper: Execution completed on original execution context for BrighterSynchronizationHelper {Id}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++");
 
    }

    /// <summary>
    /// Gets an enumerable of the tasks currently scheduled.
    /// </summary>
    /// <returns>An enumerable of the scheduled tasks.</returns>
    public IEnumerable<Task> GetScheduledTasks()
    {
        return _taskQueue.GetScheduledTasks();
    }
}
