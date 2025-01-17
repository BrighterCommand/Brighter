#region Sources

// This class is based on Stephen Cleary's AyncContext in https://github.com/StephenCleary/AsyncEx
// The original code is licensed under the MIT License (MIT) <a href="https://github.com/StephenCleary/AsyncEx/blob/master/LICENS>AyncEx license</a>
// Modifies the original approach in Brighter which only provided a synchronization synchronizationHelper, not a scheduler, and thus would
// not run continuations on the same thread as the async operation if used with ConfigureAwait(false.
// This is important for the ServiceActivator, as we want to ensure ordering on a single thread and not use the thread pool.

// Originally based on:

//Also based on:
// https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/
// https://raw.githubusercontent.com/Microsoft/vs-threading/refs/heads/main/src/Microsoft.VisualStudio.Threading/SingleThreadedSynchronizationContext.cs
// https://github.com/microsoft/referencesource/blob/master/System.Web/AspNetSynchronizationContext.cs

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// This class provides a task scheduler that causes all tasks to be executed synchronously on the current thread.
/// The synchronizationHelper and scheduler are used to run continuations on the same thread as the async operation.
/// </summary>
internal class BrighterTaskScheduler : TaskScheduler
{
    private readonly BrighterSynchronizationHelper _synchronizationHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterTaskScheduler"/> class.
    /// </summary>
    /// <param name="synchronizationHelper">The synchronizationHelper in which tasks should be executed.</param>
    public BrighterTaskScheduler(BrighterSynchronizationHelper synchronizationHelper)
    {
        _synchronizationHelper = synchronizationHelper;
    }

    /// <summary>
    /// Gets an enumerable of the tasks currently scheduled.
    /// </summary>
    /// <returns>An enumerable of the scheduled tasks.</returns>
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return _synchronizationHelper.GetScheduledTasks();
    }

    /// <summary>
    /// Queues a task to the scheduler.
    /// </summary>
    /// <param name="task">The task to be queued.</param>
    protected override void QueueTask(Task task)
    {
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: QueueTask on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
        
       var queued = _synchronizationHelper.Enqueue((Task)task, false);
       if (!queued)
   {
           Debug.IndentLevel = 1;
           Debug.WriteLine($"BrighterTaskScheduler: QueueTask Failed to queue task {task.ToString()} on {System.Threading.Thread.CurrentThread.ManagedThreadId}");
           Debug.IndentLevel = 0;
       }
    }

    /// <summary>
    /// Attempts to execute the specified task on the current thread.
    /// </summary>
    /// <param name="task">The task to be executed.</param>
    /// <param name="taskWasPreviouslyQueued">A boolean indicating whether the task was previously queued.</param>
    /// <returns>True if the task was executed; otherwise, false.</returns>
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: TryExecuteTaskInline on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
        
        return (BrighterSynchronizationHelper.Current == _synchronizationHelper) && TryExecuteTask(task);
        
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
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: DoTryExecuteTask on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
        
        TryExecuteTask(task);
    }
}
