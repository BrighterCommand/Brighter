using System;

namespace Paramore.Brighter;

/// <summary>
/// The API for message scheduler (like in-memory, Hang fire and others)
/// </summary>
public interface IAmAMessageSchedulerSync : IAmAMessageScheduler, IDisposable
{
    /// <summary>
    /// Scheduler a message to be executed the provided <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="message">The <see cref="Message"/>.</param>
    /// <param name="at">The date-time of when a message should be placed.</param>
    /// <returns>The scheduler id.</returns>
    string Schedule(Message message, DateTimeOffset at);

    /// <summary>
    /// Scheduler a message to be executed the provided <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="message">The <see cref="Message"/>.</param>
    /// <param name="delay">The <see cref="TimeSpan"/> of delay before place the message.</param>
    /// <returns>The scheduler id.</returns>
    string Schedule(Message message, TimeSpan delay);

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
