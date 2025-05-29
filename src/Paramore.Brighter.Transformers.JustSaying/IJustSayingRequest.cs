namespace Paramore.Brighter.Transformers.JustSaying;

public interface IJustSayingRequest : IRequest
{
    /// <summary>
    /// The time stamp when the message was created.
    /// </summary>
    DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// The raising component.
    /// </summary>
    string? RaisingComponent { get; set; }

    /// <summary>
    /// The current version
    /// </summary>
    string? Version { get; set; }

    /// <summary>
    /// The source IP
    /// </summary>
    /// <remarks>
    /// It's used by Just Saying
    /// </remarks>
    string? SourceIp { get; set; }

    /// <summary>
    /// The message tenant.
    /// </summary>
    string Tenant { get; set; }

    /// <summary>
    /// The conversation id.
    /// </summary>
    string? Conversation { get; set; }
}
