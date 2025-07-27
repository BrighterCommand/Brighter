using System;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes;


/// <summary>
/// An attribute used to indicate that a command processor pipeline step should 
/// unwrap messages from a CloudEvent envelope (JSON format) before processing.
/// </summary>
/// <remarks>
/// Applied to handler methods to configure CloudEvents unwrapping in a Brighter pipeline.
/// Specifies the use of <see cref="CloudEventsTransformer"/> with JSON formatting.
/// The <paramref name="step"/> parameter determines the order in the pipeline unwrapping sequence.
/// </remarks>
public class ReadFromCloudEventsAttribute(int step) : UnwrapWithAttribute(step)
{
    /// <inheritdoc />
    public override object?[] InitializerParams()
    {
        return [CloudEventFormat.Json];
    }

    /// <inheritdoc />
    public override Type GetHandlerType()
    {
        return typeof(CloudEventsTransformer);
    }
}
