namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public class AlbumAdded : Event
{
    public string Title { get; set; }
    
    public AlbumAdded(string title, string correlationId) : base(Brighter.Id.Random())
    {
        Title = title;
        CorrelationId = correlationId;
    }
}
