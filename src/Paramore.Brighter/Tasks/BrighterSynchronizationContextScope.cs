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
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// RAII-style scope that installs a <see cref="BrighterSynchronizationContext"/> on the
/// current thread and restores the prior context on dispose.
/// </summary>
internal sealed class BrighterSynchronizationContextScope : SingleDisposable<object>
{
    private readonly SynchronizationContext? _originalContext;
    private BrighterSynchronizationContext? _newContext;

    private BrighterSynchronizationContextScope(BrighterSynchronizationContext newContext, Task parentTask)
        : base(new object())
    {
#if DEBUG_CONTEXT
        Debug.WriteLine("{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{}");
        Debug.IndentLevel = 1;
        Debug.WriteLine($"Entering BrighterSynchronizationContextScope on thread {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"BrighterSynchronizationContextScope: Parent Task {parentTask.Id}");
        Debug.IndentLevel = 0;
#endif

        _newContext = newContext;
        _newContext.ParentTaskId = parentTask.Id;

        _originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(newContext);
    }

    protected override void Dispose(object context)
    {
#if DEBUG_CONTEXT
        Debug.IndentLevel = 1;
        Debug.WriteLine($"Exiting BrighterSynchronizationContextScope for task {_newContext?.ParentTaskId}");
        Debug.IndentLevel = 0;
        Debug.WriteLine("}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}");
#endif

        if (_newContext is not null)
        {
            _newContext.ParentTaskId = 0;
            _newContext = null;
        }

        SynchronizationContext.SetSynchronizationContext(_originalContext);
    }

    /// <summary>
    /// Runs <paramref name="action"/> with <paramref name="context"/> installed as the ambient
    /// synchronization context, restoring the previous context on return.
    /// </summary>
    public static void ApplyContext(BrighterSynchronizationContext? context, Task parentTask, Action action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
#else
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif

        using (new BrighterSynchronizationContextScope(context, parentTask))
            action();
    }
}
