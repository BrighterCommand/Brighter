using UserGroupManagement.ServiceLayer.CommandProcessor;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.CommandHandlers
{
    public interface IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        void AddToChain(ChainPathExplorer pathExplorer);
        TRequest Handle(TRequest request);
        IHandleRequests<TRequest> Successor { set; }
    }
}
