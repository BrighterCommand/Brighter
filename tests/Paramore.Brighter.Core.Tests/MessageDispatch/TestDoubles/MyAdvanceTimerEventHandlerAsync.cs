using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyAdvanceTimerEventHandlerAsync(FakeTimeProvider timeProvider) : RequestHandlerAsync<MyAdvanceTimerEvent>
{
    public override Task<MyAdvanceTimerEvent> HandleAsync(MyAdvanceTimerEvent advanceTimerEvent, CancellationToken cancellationToken = default)
    {
        timeProvider.Advance(TimeSpan.FromMinutes(advanceTimerEvent.AdvanceInMinutes));
        return base.HandleAsync(advanceTimerEvent, cancellationToken);
    }

}
