using System;
using UserGroupManagement.ServiceLayer.CommandProcessor;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.CommandHandlers
{
    using System.Linq;
    using System.Reflection;

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

       internal MethodInfo FindHandlerMethod()
       {
            var methods = GetType().GetMethods();
            return methods
                .Where(method => method.Name == "Handle")
                .Where(method => method.GetParameters().Count() == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest))
                .SingleOrDefault();
       }
    }
}
