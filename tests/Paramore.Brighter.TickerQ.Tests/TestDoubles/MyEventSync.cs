namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

/// <summary>
/// A separate event type for the sync-handler tests so we can register
/// <see cref="MyEventHandler"/> on this type and <see cref="MyEventHandlerAsync"/>
/// on <see cref="MyEvent"/> without Brighter's pipeline confusing the two.
/// </summary>
public class MyEventSync() : Event(Id.Random())
{
    public string Value { get; set; }
}
