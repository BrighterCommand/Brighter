using System;

namespace Paramore.Brighter;

public interface IAmAMessageSchedulerSync : IAmAMessageScheduler, IDisposable
{
    string Schedule(DateTimeOffset at, Message message, RequestContext context);
    string Schedule(TimeSpan delay, Message message, RequestContext context);
    void CancelScheduler(string id);
}
