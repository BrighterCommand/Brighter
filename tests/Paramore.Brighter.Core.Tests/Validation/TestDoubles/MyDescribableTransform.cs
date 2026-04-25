using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// A minimal transform type used as a reference in DescribeTransforms tests.
/// Not instantiated — only its Type is referenced.
/// </summary>
public class MyDescribableTransform : IAmAMessageTransform
{
    public void Dispose() { }

    public IRequestContext? Context { get; set; }

    public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }

    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }

    public Message Wrap(Message message, Publication publication) => message;

    public Message Unwrap(Message message) => message;

    public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken = default)
        => Task.FromResult(message);

    public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
        => Task.FromResult(message);
}
