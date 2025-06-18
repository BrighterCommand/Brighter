namespace Paramore.Brighter.Transformers.JustSaying;

public class JustSayingCommand : Command, IJustSayingRequest
{
    public JustSayingCommand() : this(Guid.NewGuid())
    {
    }
    
    public JustSayingCommand(Id id) : base(id)
    {
    }

    public JustSayingCommand(Guid id) : base(id)
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
    public string? Tenant { get; set; }
    
    /// <inheritdoc />
    public string? Conversation { get; set; }
}
