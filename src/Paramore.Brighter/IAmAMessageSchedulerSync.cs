using System;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter;

public interface IAmAMessageSchedulerSync : IAmAMessageScheduler, IDisposable
{
    string Schedule<TRequest>(DateTimeOffset at, SchedulerFireType fireType, TRequest request)
        where TRequest : class, IRequest;
    string Schedule<TRequest>(TimeSpan delay, SchedulerFireType fireType, TRequest request)
        where TRequest : class, IRequest;
    void CancelScheduler(string id);
}
