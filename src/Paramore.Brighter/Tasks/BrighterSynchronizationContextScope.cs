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
using System.Threading;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// A utility for managing context changes.
/// </summary>
internal sealed class  BrighterSynchronizationContextScope :  SingleDisposable<object>
{
    private readonly SynchronizationContext? _originalContext;
    private readonly bool _hasOriginalContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterSynchronizationContextScope"/> struct.
    /// </summary>
    /// <param name="newContext">The new synchronization context to set.</param>
    private BrighterSynchronizationContextScope(SynchronizationContext newContext)
        : base(new object())
    {
        // Save the original synchronization context
        _originalContext = SynchronizationContext.Current;
        _hasOriginalContext = _originalContext != null;

        // Set the new synchronization context
        SynchronizationContext.SetSynchronizationContext(newContext);
    }

    /// <summary>
    /// Restores the original synchronization context.
    /// </summary>
    protected override void Dispose(object context)
    {
        // Restore the original synchronization context
        SynchronizationContext.SetSynchronizationContext(_hasOriginalContext ? _originalContext : null);
    }
    
    /// <summary>
    /// Executes a method with the specified synchronization context, and then restores the original context.
    /// </summary>
    /// <param name="context">The original synchronization context</param>
    /// <param name="action">The action to take within the context</param>
    /// <exception cref="System.ArgumentNullException">If the action passed was null</exception>
    public static void ApplyContext(SynchronizationContext? context, Action action)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (action is null) throw new ArgumentNullException(nameof(action));

        using (new BrighterSynchronizationContextScope(context))
            action();
    }
}
