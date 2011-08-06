using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Windsor;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandProcessors.ReflectionExtensionMethods;
using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessors
{
    public class ChainofResponsibilityBuilder<TRequest> : IChainofResponsibilityBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IWindsorContainer container;
        private readonly Type implicithandlerType;

        public ChainofResponsibilityBuilder(IWindsorContainer container)
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

        private RequestHandlers<TRequest> GetHandlers()
        {
            var handlers = container.ResolveAll(implicithandlerType);
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