using System.Diagnostics;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Used with OTel to propogate the current context to a message.
/// There can be both a text and binary context propogator implementation, depending on the messaging framework
/// </summary>
/// <remarks>
/// Binary propogators are not yet implemented by OTel, so as yet we have just one implementation. However,
/// don't delete this interface as we will need it when we implement binary propogation.
/// </remarks>
public interface IAmAContextPropogator
{
    void PropogateContext(ActivityContext? context, Message message);
}
