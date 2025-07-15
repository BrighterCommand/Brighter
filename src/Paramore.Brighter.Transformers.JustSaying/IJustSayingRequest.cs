using System;
using System.Net;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// The <c>IJustSayingRequest</c> interface extends the <c>IRequest</c> interface,
/// providing a standardized contract for messages exchanged within the JustSaying messaging framework.
/// It defines common properties that are useful for tracing, auditing, and routing messages.
/// </summary>
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
    IPAddress? SourceIp { get; set; }

    /// <summary>
    /// The message tenant.
    /// </summary>
    Tenant? Tenant { get; set; }

    /// <summary>
    /// The conversation id.
    /// </summary>
    Id? Conversation { get; set; }
}
