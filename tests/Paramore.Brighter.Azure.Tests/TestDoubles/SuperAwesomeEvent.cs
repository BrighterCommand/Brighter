namespace Paramore.Brighter.Azure.Tests.TestDoubles;

public class SuperAwesomeEvent : Event
{
    public string Announcement { get; set; }

    public SuperAwesomeEvent() : base(Guid.NewGuid())
    {
    }
}
