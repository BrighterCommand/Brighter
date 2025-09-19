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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// Represents a task queue that allows tasks to be added and consumed in a thread-safe manner.
/// </summary>
internal sealed class BrighterTaskQueue : IDisposable
{
    private readonly BlockingCollection<Tuple<Task, bool>> _queue = new();

    /// <summary>
    /// Gets an enumerable that consumes and removes items from the queue.
    /// </summary>
    /// <returns>An enumerable that consumes and removes items from the queue.</returns>
    public IEnumerable<Tuple<Task, bool>> GetConsumingEnumerable()
    {
        return _queue.GetConsumingEnumerable();
    }

    /// <summary>
    /// Gets an enumerable of the tasks currently scheduled in the queue.
    /// </summary>
    /// <returns>An enumerable of the scheduled tasks.</returns>
    internal IEnumerable<Task> GetScheduledTasks()
    {
        foreach (var item in _queue)
            yield return item.Item1;
    }

    /// <summary>
    /// Attempts to add a task to the queue.
    /// </summary>
    /// <param name="item">The task to be added.</param>
    /// <param name="propagateExceptions">Indicates whether to propagate exceptions.</param>
    /// <returns>True if the task was added successfully; otherwise, false.</returns>
    public bool TryAdd(Task item, bool propagateExceptions)
    {
        try
        {
#if DEBUG_CONTEXT
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterTaskQueue; Adding task: {item.Id} to queue");
            Debug.IndentLevel = 0;
#endif
            
            return _queue.TryAdd(Tuple.Create(item, propagateExceptions));
        }
        catch (InvalidOperationException)
        {
#if DEBUG_CONTEXT
            Debug.IndentLevel = 1;
            Debug.WriteLine($"BrighterTaskQueue; TaskQueue is already marked as complete for adding. Failed to add task: {item.Id}");
            Debug.IndentLevel = 0;
#endif
            
            return false;
        }
    }

    /// <summary>
    /// Marks the queue as not accepting any more additions.
    /// </summary>
    public void CompleteAdding()
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"BrighterTaskQueue; Complete adding to queue");
        Debug.IndentLevel = 0;
#endif
        
        _queue.CompleteAdding();
    }

    /// <summary>
    /// Disposes the task queue and releases all resources.
    /// </summary>
    public void Dispose()
    {
        _queue.Dispose();
    }
}
