using System;
using System.Collections.Generic;
using System.Linq;

namespace paramore.commandprocessor
{
    internal class ChainofResponsibilityBuilder<TRequest> : IChainofResponsibilityBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IAmAnInversionOfControlContainer  container;
        private readonly Type implicithandlerType;

        public ChainofResponsibilityBuilder(IAmAnInversionOfControlContainer  container)
        {
            this.container = container;

            var handlerGenericType = typeof(IHandleRequests<>);

            implicithandlerType = handlerGenericType.MakeGenericType(typeof(TRequest));

        }

        public Chains<TRequest> Build()
        {
            var handlers = GetHandlers();
            
            var chains = new Chains<TRequest>();
            foreach (var handler in handlers)
            {
                chains.Add(BuildChain(handler));
            }

            return chains;
        }

        private IEnumerable<RequestHandler<TRequest>> GetHandlers()
        {
            var handlers = container.ResolveAll(implicithandlerType, true);
            return new RequestHandlers<TRequest>(handlers);
        }

        private IHandleRequests<TRequest> BuildChain(RequestHandler<TRequest> implicitHandler)
        {
            var preAttributes = 
                implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.Before)
                .OrderByDescending(attribute => attribute.Step); 

            var firstInChain = PushOntoChain(preAttributes, implicitHandler);
            
            var postAttributes = 
                implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.After)
                .OrderByDescending(attribute => attribute.Step);

            AppendToChain(postAttributes, implicitHandler);
            return firstInChain;
        }

        private void AppendToChain(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler)
        {
            var lastInChain = implicitHandler;
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container).CreateRequestHandler();
                lastInChain.Successor = decorator;
                lastInChain = decorator;
            }
        }

        private IHandleRequests<TRequest> PushOntoChain(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInChain)
        {
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container).CreateRequestHandler();
                decorator.Successor = lastInChain;
                lastInChain = decorator;
            }
            return lastInChain;
        }
    }
}