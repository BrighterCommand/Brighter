using System;

namespace Paramore.Brighter;

/// <summary>
/// The API for request scheduler (like in-memory, Hang fire and others)
/// </summary>
public interface IAmARequestSchedulerSync : IAmARequestScheduler
{
    /// <summary>
    /// Scheduler a message to be executed the provided <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="request">The request to be scheduler</param>
    /// <param name="type">The <see cref="RequestSchedulerType"/>.</param>
    /// <param name="at">The date-time of when a message should be placed.</param>
    /// <returns>The scheduler id.</returns>
    string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest;

    /// <summary>
    /// Scheduler a message to be executed the provided <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="request">The request to be scheduler</param>
    /// <param name="type">The <see cref="RequestSchedulerType"/>.</param>
    /// <param name="delay">The <see cref="TimeSpan"/> of delay before place the message.</param>
    /// <returns>The scheduler id.</returns>
    string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest;

    /// <summary>
    /// ReScheduler a message.
    /// </summary>
    /// <param name="schedulerId">The scheduler id.</param>
    /// <param name="at">The date-time of when a message should be placed.</param>
    /// <returns>true if it could be re-scheduler, otherwise false.</returns>
    bool ReScheduler(string schedulerId, DateTimeOffset at);

    /// <summary>
    /// ReScheduler a message.
    /// </summary>
    /// <param name="schedulerId">The scheduler id.</param>
    /// <param name="delay">The <see cref="TimeSpan"/> of delay before place the message.</param>
    /// <returns>true if it could be re-scheduler, otherwise false.</returns>
    bool ReScheduler(string schedulerId, TimeSpan delay);

    /// <summary>
    /// Cancel the scheduler message
    /// </summary>
    /// <param name="id">The scheduler id.</param>
    void Cancel(string id);
}
