using System;
using UserGroupManagement.ServiceLayer.CommandProcessor;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.CommandHandlers
{
    public abstract class RequestHandler<TRequest> : IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        private IHandleRequests<TRequest> _successor;

        public IHandleRequests<TRequest> Successor
        {
            set { _successor = value; }
        }

        public void AddToChain(ChainPathExplorer pathExplorer)
        {
            pathExplorer.AddToPath(Name());
            if (_successor != null)
            {
                _successor.AddToChain(pathExplorer);
            }
        }

        public virtual TRequest Handle(TRequest request)
        {
            if (_successor != null)
            {
                return _successor.Handle(request);
            }

            return request;
        }

       protected HandlerName Name()
       {
           return new HandlerName(this.GetType().Name);
       }

    }
}
