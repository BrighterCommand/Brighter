#nullable enable
using Paramore.Brighter;

namespace HelloWorld;

/// <summary>
/// A do-nothing transform purely to exercise the source generator's transform discovery.
/// </summary>
public sealed class NoOpTransformer : IAmAMessageTransform
{
    public IRequestContext? Context { get; set; }

    public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }

    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }

    public Message Wrap(Message message, Publication publication) => message;

    public Message Unwrap(Message message) => message;

    public void Dispose() { }
}
