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

        public IRequestContext Context {get; set; }

        public void DescribePath(IAmAPipelineTracer pathExplorer)
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

            //default is just to do nothing - use this if you need to pass data from an attribute into a handler
        public virtual void InitializeFromAttributeParams(params object[] initializerList) {}

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
