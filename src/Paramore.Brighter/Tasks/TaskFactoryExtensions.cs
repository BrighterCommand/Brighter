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
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// Provides extension methods for the TaskFactory class to run tasks with various actions and functions.
    /// </summary>
    public static class TaskFactoryExtensions
    {
        /// <summary>
        /// Runs a task with the specified action.
        /// </summary>
        /// <param name="this">The TaskFactory instance.</param>
        /// <param name="action">The action to run.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the TaskFactory or action is null.</exception>
        public static Task Run(this TaskFactory @this, Action action)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return @this.StartNew(action, @this.CancellationToken, @this.CreationOptions | TaskCreationOptions.DenyChildAttach, @this.Scheduler ?? TaskScheduler.Default);
        }

        /// <summary>
        /// Runs a task with the specified function that returns a result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="this">The TaskFactory instance.</param>
        /// <param name="action">The function to run.</param>
        /// <returns>A Task that represents the asynchronous operation and contains the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the TaskFactory or action is null.</exception>
        public static Task<TResult> Run<TResult>(this TaskFactory @this, Func<TResult> action)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return @this.StartNew(action, @this.CancellationToken, @this.CreationOptions | TaskCreationOptions.DenyChildAttach, @this.Scheduler ?? TaskScheduler.Default);
        }

        /// <summary>
        /// Runs a task with the specified asynchronous function.
        /// </summary>
        /// <param name="this">The TaskFactory instance.</param>
        /// <param name="action">The asynchronous function to run.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the TaskFactory or action is null.</exception>
        public static Task Run(this TaskFactory @this, Func<Task> action)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return @this.StartNew(action, @this.CancellationToken, @this.CreationOptions | TaskCreationOptions.DenyChildAttach, @this.Scheduler ?? TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// Runs a task with the specified asynchronous function that returns a result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="this">The TaskFactory instance.</param>
        /// <param name="action">The asynchronous function to run.</param>
        /// <returns>A Task that represents the asynchronous operation and contains the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the TaskFactory or action is null.</exception>
        public static Task<TResult> Run<TResult>(this TaskFactory @this, Func<Task<TResult>> action)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return @this.StartNew(action, @this.CancellationToken, @this.CreationOptions | TaskCreationOptions.DenyChildAttach, @this.Scheduler ?? TaskScheduler.Default).Unwrap();
        }
    }
}
