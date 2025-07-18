using System;

namespace Paramore.Brighter.Transformers.MassTransit;

/// <summary>
/// Attribute that convert MassTransit envelop message into Brighter message.
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
/// </remarks>

[AttributeUsage(AttributeTargets.Method, Inherited = false)] 
public class MassTransitUnwrapAttribute(int step) : UnwrapWithAttribute(step)
{
    /// <inheritdoc />
    public override Type GetHandlerType()
    {
        return typeof(MassTransitTransform);
    }

}
