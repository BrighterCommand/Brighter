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
/// Task scheduler that queues tasks to a <see cref="BrighterAsyncContext"/> so they execute
/// on that context's single thread.
/// </summary>
internal sealed class BrighterTaskScheduler : TaskScheduler
{
    private readonly BrighterAsyncContext _asyncContext;

    public BrighterTaskScheduler(BrighterAsyncContext asyncContext)
    {
#if NET
        ArgumentNullException.ThrowIfNull(asyncContext);
#else
        if (asyncContext is null) throw new ArgumentNullException(nameof(asyncContext));
#endif
        _asyncContext = asyncContext;
    }

    /// <inheritdoc />
    protected override IEnumerable<Task> GetScheduledTasks() => _asyncContext.GetScheduledTasks();

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: QueueTask on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif

        _asyncContext.Enqueue(task, propagateExceptions: false);
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: TryExecuteTaskInline on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif

        return BrighterAsyncContext.Current == _asyncContext && TryExecuteTask(task);
    }

    /// <inheritdoc />
    public override int MaximumConcurrencyLevel => 1;

    /// <summary>
    /// Executes the specified task on the calling thread.
    /// </summary>
    public void DoTryExecuteTask(Task task)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskScheduler: DoTryExecuteTask on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.IndentLevel = 0;
#endif

        TryExecuteTask(task);
    }
}
