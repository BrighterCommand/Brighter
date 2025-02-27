using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// The async API for message scheduler (like in-memory, Hang fire and others)
/// </summary>
public interface IAmARequestSchedulerAsync : IAmARequestScheduler 
{
    /// <summary>
    /// Scheduler a message to be executed the provided <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="request">The request to be scheduler</param>
    /// <param name="type">The <see cref="RequestSchedulerType"/>.</param>
    /// <param name="at">The date-time of when a message should be placed.</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    /// <returns>The scheduler id.</returns>
    Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;

    /// <summary>
    /// Scheduler a message to be executed the provided <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="request">The request to be scheduler</param>
    /// <param name="type">The <see cref="RequestSchedulerType"/>.</param>
    /// <param name="delay">The <see cref="TimeSpan"/> of delay before place the message.</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    /// <returns>The scheduler id.</returns>
    Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;

    /// <summary>
    /// ReScheduler a message.
    /// </summary>
    /// <param name="schedulerId">The scheduler id.</param>
    /// <param name="at">The date-time of when a message should be placed.</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    /// <returns>true if it could be re-scheduler, otherwise false.</returns>
    Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    /// ReScheduler a message.
    /// </summary>
    /// <param name="schedulerId">The scheduler id.</param>
    /// <param name="delay">The <see cref="TimeSpan"/> of delay before place the message.</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    /// <returns>true if it could be re-scheduler, otherwise false.</returns>
    Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel the scheduler message
    /// </summary>
    /// <param name="id">The scheduler id.</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    /// <returns></returns>
    Task CancelAsync(string id, CancellationToken cancellationToken = default);
}
