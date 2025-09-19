#region Sources

// This class is based on Stephen Cleary's AyncContext in https://github.com/StephenCleary/AsyncEx
// The original code is licensed under the MIT License (MIT) <a href="https://github.com/StephenCleary/AsyncEx/blob/master/LICENS>AyncEx license</a>
// Modifies the original approach in Brighter which only provided a synchronization synchronizationHelper, not a scheduler, and thus would
// not run continuations on the same thread as the async operation if used with ConfigureAwait(false.
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
/// This class provides a task scheduler that causes all tasks to be executed synchronously on the current thread.
/// The synchronizationHelper and scheduler are used to run continuations on the same thread as the async operation.
/// </summary>
internal sealed class BrighterTaskScheduler : TaskScheduler
{
    private readonly BrighterAsyncContext _asyncContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterTaskScheduler"/> class.
    /// </summary>
    /// <param name="asyncContext">The synchronizationHelper in which tasks should be executed.</param>
    public BrighterTaskScheduler(BrighterAsyncContext asyncContext)
    {
        _asyncContext = asyncContext;
    }

    /// <summary>
    /// Gets an enumerable of the tasks currently scheduled.
    /// </summary>
    /// <returns>An enumerable of the scheduled tasks.</returns>
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return _asyncContext.GetScheduledTasks();
    }

    /// <summary>
    /// Queues a task to the scheduler.
    /// </summary>
    /// <param name="task">The task to be queued.</param>
    protected override void QueueTask(Task task)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: QueueTask on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif
        
       _asyncContext.Enqueue((Task)task, false);
       
       // If we fail to add to the queue, just drop the Task.
    }
    
    /// <summary>
    /// Attempts to execute the specified task on the current thread.
    /// </summary>
    /// <param name="task">The task to be executed.</param>
    /// <param name="taskWasPreviouslyQueued">A boolean indicating whether the task was previously queued.</param>
    /// <returns>True if the task was executed; otherwise, false.</returns>
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: TryExecuteTaskInline on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif

        return BrighterAsyncContext.Current == _asyncContext && TryExecuteTask(task);
    }

    /// <summary>
    /// Gets the maximum concurrency level supported by this scheduler.
    /// </summary>
    public override int MaximumConcurrencyLevel
    {
        get { return 1; }
    }

    /// <summary>
    /// Attempts to execute the specified task.
    /// </summary>
    /// <param name="task">The task to be executed.</param>
    public void DoTryExecuteTask(Task task)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: DoTryExecuteTask on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif
        
        TryExecuteTask(task);
    }
    
    /// <summary>
    /// In a new thread, attempts to execute the specified task.
    /// </summary>
    /// <remarks>
    /// This is a little "Hail Mary" to try and execute a task that has failed to queue because we have already completed the synchronization context.
    /// Seems to be caused by an Exection Context that has our scheduler as the default, as it fools ConfigureAwait
    /// </remarks>
    /// <param name="obj"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void TryExecuteNewThread(object? obj)
    {
        var task = obj as Task;
        if (task  == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: Use TryExecuteNewThread for {task} on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif
        
        TryExecuteTask(task);
    }
}
