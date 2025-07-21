using System;

namespace Paramore.Brighter.Transformers.MassTransit;

/// <summary>
/// Configures message routing and type settings for MassTransit message classes to facilitate 
/// integration between Brighter and MassTransit.
/// </summary>
/// <remarks>
/// Apply this attribute to message classes to explicitly define routing endpoints and message type 
/// identifiers. This is particularly useful when working with legacy contracts or external systems [[1]][[7]].
/// The <see cref="AttributeUsageAttribute.Inherited"/> flag is explicitly set to false to prevent unintended 
/// inheritance behavior in derived message types.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public abstract class MassTransitMessageAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the destination address where messages of this type should be sent.
    /// </summary>
    /// <remarks>
    /// This corresponds to MassTransit's endpoint configuration for message routing. 
    /// Format should follow transport-specific conventions (e.g., "rabbitmq://host/queue").
    /// </remarks>
    public string? DestinationAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the fault address where exception messages should be published.
    /// </summary>
    /// <remarks>
    /// Used by MassTransit's fault handling infrastructure to route error messages.
    /// If unset, defaults to the default error queue configuration.
    /// </remarks>
    public string? FaultAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the response address where reply messages should be sent.
    /// </summary>
    /// <remarks>
    /// Configures the ReplyTo header in message envelopes for request/response patterns.
    /// </remarks>
    public string? ResponseAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the source address identifier for this message type.
    /// </summary>
    /// <remarks>
    /// Identifies the originating endpoint in message metadata. Useful for tracing
    /// and auditing in distributed systems.
    /// </remarks>
    public string? SourceAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the message type identifiers for this message.
    /// </summary>
    /// <remarks>
    /// These URNs (e.g., "urn:message:MyNamespace.MyMessage") are used by MassTransit for 
    /// type resolution during serialization. If unset, defaults to the message's 
    /// fully qualified type name.
    /// </remarks>
    public string[]? MessageType { get;set; }
}
