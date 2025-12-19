using System;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyAdvanceTimerEvent(int advanceInMinutes) : Event(Brighter.Id.Random())
{
    public int AdvanceInMinutes { get; } = advanceInMinutes;
}
