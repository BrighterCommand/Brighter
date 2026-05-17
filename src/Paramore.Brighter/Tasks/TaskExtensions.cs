#region Sources

// These extension methods are based on Stephen Cleary's Nito.AsyncEx synchronous task
// extensions, see https://github.com/StephenCleary/AsyncEx/blob/master/src/Nito.AsyncEx.Tasks/SynchronousTaskExtensions.cs
// The original code is licensed under the MIT License (MIT).

#endregion

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

public static class TaskExtensions
{
    /// <summary>
    /// Waits for the task to complete, unwrapping any exception (the original exception is
    /// rethrown directly, not wrapped in an <see cref="AggregateException"/>).
    /// </summary>
    public static void WaitAndUnwrapException(this Task task)
    {
#if NET
        ArgumentNullException.ThrowIfNull(task);
#else
        if (task is null) throw new ArgumentNullException(nameof(task));
#endif
        task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Waits for the task to complete, unwrapping any exception. Observes the supplied
    /// <paramref name="cancellationToken"/>.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// The <paramref name="cancellationToken"/> was cancelled before the task completed, or
    /// the task itself raised an <see cref="OperationCanceledException"/>.
    /// </exception>
    public static void WaitAndUnwrapException(this Task task, CancellationToken cancellationToken)
    {
#if NET
        ArgumentNullException.ThrowIfNull(task);
        task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
#else
        if (task is null) throw new ArgumentNullException(nameof(task));
        try
        {
            task.Wait(cancellationToken);
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
#endif
    }

    /// <summary>
    /// Waits for the task to complete, unwrapping any exception and returning its result.
    /// </summary>
    public static TResult WaitAndUnwrapException<TResult>(this Task<TResult> task)
    {
#if NET
        ArgumentNullException.ThrowIfNull(task);
#else
        if (task is null) throw new ArgumentNullException(nameof(task));
#endif
        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Waits for the task to complete, unwrapping any exception and returning its result.
    /// Observes the supplied <paramref name="cancellationToken"/>.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// The <paramref name="cancellationToken"/> was cancelled before the task completed, or
    /// the task itself raised an <see cref="OperationCanceledException"/>.
    /// </exception>
    public static TResult WaitAndUnwrapException<TResult>(this Task<TResult> task, CancellationToken cancellationToken)
    {
#if NET
        ArgumentNullException.ThrowIfNull(task);
        return task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
#else
        if (task is null) throw new ArgumentNullException(nameof(task));
        try
        {
            task.Wait(cancellationToken);
            return task.Result;
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
#endif
    }

    /// <summary>
    /// Waits for the task to complete, silently observing and discarding any exception.
    /// </summary>
    public static void WaitWithoutException(this Task task)
    {
#if NET
        ArgumentNullException.ThrowIfNull(task);
#else
        if (task is null) throw new ArgumentNullException(nameof(task));
#endif
        try
        {
            task.Wait();
        }
        catch (AggregateException)
        {
        }
    }

    /// <summary>
    /// Waits for the task to complete, silently observing and discarding any exception.
    /// Observes the supplied <paramref name="cancellationToken"/>.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// The <paramref name="cancellationToken"/> was cancelled before the task completed.
    /// </exception>
    public static void WaitWithoutException(this Task task, CancellationToken cancellationToken)
    {
#if NET
        ArgumentNullException.ThrowIfNull(task);
#else
        if (task is null) throw new ArgumentNullException(nameof(task));
#endif
        try
        {
            task.Wait(cancellationToken);
        }
        catch (AggregateException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
