using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A transform that records how many times it has been disposed. Used to prove that a
/// released transient transform is not retained by the factory's service scope (a retained
/// instance is disposed a second time when the scope is finally disposed).
/// </summary>
public class DisposeCountingTransform : IAmAMessageTransformAsync, IAmAMessageTransform
{
    private int _disposeCount;

    public int DisposeCount => _disposeCount;

    public IRequestContext Context { get; set; }

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
    }

    public void InitializeWrapFromAttributeParams(params object[] initializerList) { }

    public void InitializeUnwrapFromAttributeParams(params object[] initializerList) { }

    public Message Wrap(Message message, Publication publication) => message;

    public Message Unwrap(Message message) => message;

    public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
        => Task.FromResult(message);

    public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
        => Task.FromResult(message);
}
