using System;

namespace Paramore.Brighter.Transformers.MassTransit;

/// <summary>
/// Attribute that convert Brighter message into MassTransit envelop message.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute on message mapper methods to enable MassTransit compatibility through the <see cref="MassTransitTransform"/>.
/// It configures routing and metadata headers required by MassTransit's default envelope format.
/// </para>
/// <para>
/// <strong>Performance Note:</strong> Prefer <see cref="MassTransitMessageMapper{TMessage}"/> for better performance. 
/// This attribute enables dynamic JSON manipulation via <see cref="MassTransitTransform"/>, which has higher overhead 
/// compared to strongly-typed mapping.
/// </para>
/// <para>
/// Configuration parameters (e.g., DestinationAddress) take precedence over context values. If unset, values are sourced 
/// from message context or defaults (e.g., publication source for SourceAddress).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)] 
public class MassTransitWrapAttribute(int step) : WrapWithAttribute(step)
{
    /// <summary>
    /// Gets or sets the destination address for MassTransit message routing.
    /// </summary>
    public string? DestinationAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the fault address for error handling in MassTransit.
    /// </summary> 
    public string? FaultAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the response address for request-response patterns.
    /// </summary>
    public string? ResponseAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the source address for message origin tracking.
    /// </summary>
    public string? SourceAddress { get; set; }
    
    /// <summary>
    /// Gets or sets message type identifiers for MassTransit routing.
    /// </summary>
    public string[]? MessageType { get;set; }

    public override object?[] InitializerParams()
    {
        return [ DestinationAddress, FaultAddress, ResponseAddress, SourceAddress, MessageType ];
    }

    /// <inheritdoc />
    public override Type GetHandlerType()
    {
        return typeof(MassTransitTransform);
    }
}
