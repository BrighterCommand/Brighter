using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

public interface IAmAMessageSchedulerAsync : IAmAMessageScheduler, IDisposable
{
    Task<string> ScheduleAsync(DateTimeOffset at, Message message, RequestContext context, CancellationToken cancellationToken = default);
    Task<string> ScheduleAsync(TimeSpan delay, Message message, RequestContext context, CancellationToken cancellationToken = default);
    Task CancelSchedulerAsync(string id, CancellationToken cancellationToken = default);
}
