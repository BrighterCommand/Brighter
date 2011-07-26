using Paramore.Services.CommandProcessors;
using Paramore.Services.Common;

namespace Paramore.Services.CommandHandlers
{
    public interface IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        void DescribePath(ChainPathExplorer pathExplorer);
        TRequest Handle(TRequest request);
        IHandleRequests<TRequest> Successor { set; }
    }
}
