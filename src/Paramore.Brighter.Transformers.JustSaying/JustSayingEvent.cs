namespace Paramore.Brighter.Transformers.JustSaying;

public class JustSayingEvent : Event, IJustSayingRequest
{
    public JustSayingEvent() : this(Guid.NewGuid())
    {
        
    }
    public JustSayingEvent(Id id) : base(id)
    {
    }

    public JustSayingEvent(Guid id) : base(id)
    {
    }

    /// <inheritdoc />
    public DateTimeOffset TimeStamp { get; set; }
    
    /// <inheritdoc />
    public string? RaisingComponent { get; set; }
    
    /// <inheritdoc />
    public string? Version { get; set; }
    
    /// <inheritdoc />
    public string? SourceIp { get; set; }
    
    /// <inheritdoc />
    public string Tenant { get; set; } = "all";
    
    /// <inheritdoc />
    public string? Conversation { get; set; }
}
