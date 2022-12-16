using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests;

public class TestTransform : IAmAMessageTransformAsync
{
    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public void InitializeWrapFromAttributeParams(params object[] initializerList)
    {
        throw new System.NotImplementedException();
    }

    public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Message> WrapAsync(Message message, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}
