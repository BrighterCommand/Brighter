using System.Linq;
using System.Reflection;

namespace paramore.commandprocessor
{
    public abstract class RequestHandler<TRequest> : IHandleRequests<TRequest> where TRequest : class, IRequest
    {
        private IHandleRequests<TRequest> _successor;

        public IHandleRequests<TRequest> Successor
        {
            set { _successor = value; }
        }

        public void DescribePath(ChainPathExplorer pathExplorer)
        {
            pathExplorer.AddToPath(Name());
            if (_successor != null)
            {
                _successor.DescribePath(pathExplorer);
            }
        }

        public virtual TRequest Handle(TRequest command)
        {
            if (_successor != null)
            {
                return _successor.Handle(command);
            }

            return command;
        }

       protected HandlerName Name()
       {
           return new HandlerName(GetType().Name);
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
