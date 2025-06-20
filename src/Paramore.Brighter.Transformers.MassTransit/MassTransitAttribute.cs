using System;

namespace Paramore.Brighter.Transformers.MassTransit;

[AttributeUsage(AttributeTargets.Method, Inherited = false)] 
public class MassTransitAttribute(int step) : WrapWithAttribute(step)
{
    public string? DestinationAddress { get; set; }
    public string? FaultAddress { get; set; }
    public string? ResponseAddress { get; set; }
    public string? SourceAddress { get; set; }
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
