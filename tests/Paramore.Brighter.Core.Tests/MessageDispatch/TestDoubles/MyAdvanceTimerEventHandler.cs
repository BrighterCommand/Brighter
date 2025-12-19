using System;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyAdvanceTimerEventHandler(FakeTimeProvider timeProvider) : RequestHandler<MyAdvanceTimerEvent>
{
    public override MyAdvanceTimerEvent Handle(MyAdvanceTimerEvent advanceTimerEvent)
    {
        timeProvider.Advance(TimeSpan.FromMinutes(advanceTimerEvent.AdvanceInMinutes));
        return base.Handle(advanceTimerEvent);
    }
}
