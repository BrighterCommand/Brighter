using System;
using System.Collections.Generic;
using System.Linq;
using paramore.commandprocessor.extensions;

namespace paramore.commandprocessor
{
    internal class ChainofResponsibilityBuilder<TRequest> : IChainofResponsibilityBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IAdaptAnInversionOfControlContainer  container;
        private readonly Type implicithandlerType;

        public ChainofResponsibilityBuilder(IAdaptAnInversionOfControlContainer  container)
        {
            this.container = container;

            var handlerGenericType = typeof(IHandleRequests<>);

            implicithandlerType = handlerGenericType.MakeGenericType(typeof(TRequest));

        }

        public Chains<TRequest> Build(IRequestContext requestContext)
        {
            var handlers = GetHandlers();
            
            var chains = new Chains<TRequest>();
            foreach (var handler in handlers)
            {
                chains.Add(BuildChain(handler, requestContext));
            }

            return chains;
        }

        private IEnumerable<RequestHandler<TRequest>> GetHandlers()
        {
            var handlers = new RequestHandlers<TRequest>(container.ResolveAll(implicithandlerType, true));
            return handlers;
        }

        private IHandleRequests<TRequest> BuildChain(RequestHandler<TRequest> implicitHandler, IRequestContext requestContext)
        {
            implicitHandler.Context = requestContext;

            var preAttributes = 
                implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.Before)
                .OrderByDescending(attribute => attribute.Step); 

            var firstInChain = PushOntoChain(preAttributes, implicitHandler, requestContext);
            
            var postAttributes = 
                implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.After)
                .OrderByDescending(attribute => attribute.Step);

            AppendToChain(postAttributes, implicitHandler, requestContext);
            return firstInChain;
        }

        private void AppendToChain(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler, IRequestContext requestContext)
        {
            IHandleRequests<TRequest> lastInChain = implicitHandler;
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container, requestContext).CreateRequestHandler();
                lastInChain.Successor = decorator;
                lastInChain = decorator;
            }
        }

        private IHandleRequests<TRequest> PushOntoChain(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInChain, IRequestContext requestContext)
        {
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, container, requestContext).CreateRequestHandler();
                decorator.Successor = lastInChain;
                lastInChain = decorator;
            }
            return lastInChain;
        }
    }
}