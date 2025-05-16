using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Propogates the current trace context to a message using the OTel text map propogator.
/// </summary>
public class TextContextPropogator : IAmAContextPropogator
{
    /// <summary>
    /// Propogate the context to the message using the OTel text map propogator
    /// We will add the context to the <see cref="MessageHeader"/>'s TraceParent and TraceState properties
    /// </summary>
    /// <param name="context"></param>
    /// <param name="message"></param>
    public void PropogateContext(ActivityContext? context, Message message)
    {
        var propogator = Propagators.DefaultTextMapPropagator;
        propogator.Inject( new PropagationContext(context ?? default, OpenTelemetry.Baggage.Current), message, Message.PropogateContext );
    }
}
