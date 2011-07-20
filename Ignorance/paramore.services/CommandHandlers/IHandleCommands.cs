using Paramore.Services.CommandProcessor;
using Paramore.Services.Common;

namespace Paramore.Services.CommandHandlers
{
    public interface IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        void AddToChain(ChainPathExplorer pathExplorer);
        TRequest Handle(TRequest request);
        IHandleRequests<TRequest> Successor { set; }
    }
}
