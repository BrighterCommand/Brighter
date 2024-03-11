namespace Paramore.Brighter.Azure.Tests.TestDoubles;

public class SuperAwesomeEvent(string announcement) : Event(Guid.NewGuid())
{
    public string Announcement { get; set; } = announcement;
}
