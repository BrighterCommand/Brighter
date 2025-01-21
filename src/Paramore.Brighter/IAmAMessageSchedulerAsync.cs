using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter;

public interface IAmAMessageSchedulerAsync : IAmAMessageScheduler, IDisposable
{
    Task<string> ScheduleAsync<TRequest>(DateTimeOffset at, SchedulerFireType fireType, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;
    Task<string> ScheduleAsync<TRequest>(TimeSpan delay, SchedulerFireType fireType, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;
    Task CancelSchedulerAsync(string id, CancellationToken cancellationToken = default);
}
