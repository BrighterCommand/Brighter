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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// Thread-safe queue of tasks awaiting execution, paired with a flag indicating whether
/// exceptions should be propagated back to the executor.
/// </summary>
internal sealed class BrighterTaskQueue : IDisposable
{
    private readonly BlockingCollection<(Task Task, bool PropagateExceptions)> _queue = new();

    /// <summary>
    /// Enumerates and removes items as they become available. Blocks until the queue is
    /// completed for adding.
    /// </summary>
    public IEnumerable<(Task Task, bool PropagateExceptions)> GetConsumingEnumerable() =>
        _queue.GetConsumingEnumerable();

    /// <summary>
    /// A snapshot of tasks currently scheduled in the queue. Returns an empty enumerable if
    /// the queue has already been disposed.
    /// </summary>
    internal IEnumerable<Task> GetScheduledTasks()
    {
        try
        {
            // ToArray is thread-safe against concurrent Add/Take on BlockingCollection;
            // enumerating the live queue directly via foreach is not.
            return _queue.ToArray().Select(static item => item.Task);
        }
        catch (ObjectDisposedException)
        {
            return Array.Empty<Task>();
        }
    }

    /// <summary>
    /// Attempts to add a task to the queue.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the task was added; <c>false</c> if the queue has been completed for
    /// adding or already disposed.
    /// </returns>
    public bool TryAdd(Task item, bool propagateExceptions)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskQueue: Adding task {item.Id} to queue");
        Debug.IndentLevel = 0;
#endif
        try
        {
            return _queue.TryAdd((item, propagateExceptions));
        }
        // ObjectDisposedException : InvalidOperationException - catch the derived type first.
        // Owning context has been disposed; absorb so stray async continuations posting back
        // after Run returns do not crash the caller.
        catch (ObjectDisposedException) { return false; }
        // Completed for adding - queue is still alive but no longer accepting.
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>
    /// Marks the queue as not accepting any further additions. Idempotent and safe to call
    /// after <see cref="Dispose"/>.
    /// </summary>
    public void CompleteAdding()
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine("BrighterTaskQueue: Complete adding to queue");
        Debug.IndentLevel = 0;
#endif
        try
        {
            _queue.CompleteAdding();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed - nothing to do.
        }
    }

    /// <inheritdoc />
    public void Dispose() => _queue.Dispose();
}
