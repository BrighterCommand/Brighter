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
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// Extension methods that add <c>Task.Run</c>-style overloads to <see cref="TaskFactory"/>,
/// honouring the factory's own cancellation token, creation options and scheduler.
/// </summary>
public static class TaskFactoryExtensions
{
    /// <summary>
    /// Runs <paramref name="action"/> on the factory's scheduler.
    /// </summary>
    public static Task Run(this TaskFactory @this, Action action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(@this);
        ArgumentNullException.ThrowIfNull(action);
#else
        if (@this is null) throw new ArgumentNullException(nameof(@this));
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif

        return @this.StartNew(
            action,
            @this.CancellationToken,
            @this.CreationOptions | TaskCreationOptions.DenyChildAttach,
            @this.Scheduler ?? TaskScheduler.Default);
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the factory's scheduler and returns its result.
    /// </summary>
    public static Task<TResult> Run<TResult>(this TaskFactory @this, Func<TResult> action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(@this);
        ArgumentNullException.ThrowIfNull(action);
#else
        if (@this is null) throw new ArgumentNullException(nameof(@this));
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif

        return @this.StartNew(
            action,
            @this.CancellationToken,
            @this.CreationOptions | TaskCreationOptions.DenyChildAttach,
            @this.Scheduler ?? TaskScheduler.Default);
    }

    /// <summary>
    /// Runs the async delegate <paramref name="action"/> on the factory's scheduler.
    /// </summary>
    public static Task Run(this TaskFactory @this, Func<Task> action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(@this);
        ArgumentNullException.ThrowIfNull(action);
#else
        if (@this is null) throw new ArgumentNullException(nameof(@this));
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif

        return @this.StartNew(
            action,
            @this.CancellationToken,
            @this.CreationOptions | TaskCreationOptions.DenyChildAttach,
            @this.Scheduler ?? TaskScheduler.Default).Unwrap();
    }

    /// <summary>
    /// Runs the async delegate <paramref name="action"/> on the factory's scheduler and returns its result.
    /// </summary>
    public static Task<TResult> Run<TResult>(this TaskFactory @this, Func<Task<TResult>> action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(@this);
        ArgumentNullException.ThrowIfNull(action);
#else
        if (@this is null) throw new ArgumentNullException(nameof(@this));
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif

        return @this.StartNew(
            action,
            @this.CancellationToken,
            @this.CreationOptions | TaskCreationOptions.DenyChildAttach,
            @this.Scheduler ?? TaskScheduler.Default).Unwrap();
    }
}
