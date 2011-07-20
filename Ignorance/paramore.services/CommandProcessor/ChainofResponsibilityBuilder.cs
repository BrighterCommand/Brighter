using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Windsor;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandProcessor.ReflectionExtensionMethods;
using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessor
{
    public class ChainofResponsibilityBuilder<TRequest> where TRequest : class, IRequest
    {
        private readonly IWindsorContainer _container;
        private readonly Type _implicithandlerType;

        public ChainofResponsibilityBuilder(IWindsorContainer container)
        {
            _container = container;

            var handlerGenericType = typeof(IHandleRequests<>);

            _implicithandlerType = handlerGenericType.MakeGenericType(typeof(TRequest));

        }

        public Chains<TRequest> Build()
        {
            var handlers = GetHandlers();
            
            handlers.CheckForMissingHandler();

            var chains = new Chains<TRequest>();
            foreach (var handler in handlers)
            {
                chains.Add(BuildChain(handler));
            }

            return chains;
        }

        private RequestHandlers<TRequest> GetHandlers()
        {
            return new RequestHandlers<TRequest>(_container.ResolveAll(_implicithandlerType));
        }

        private IHandleRequests<TRequest> BuildChain(RequestHandler<TRequest> implicitHandler)
        {
            var preAttributes = implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.Before).OrderByDescending(attribute => attribute.Step); 

            var firstInChain = PushOntoChain(preAttributes, implicitHandler);
            
            var postAttributes = implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.After).OrderByDescending(attribute => attribute.Step);

            AppendToChain(postAttributes, implicitHandler);
            return firstInChain;
        }

        private void AppendToChain(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> implicitHandler)
        {
            var lastInChain = implicitHandler;
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, _container).CreateRequestHandler();
                lastInChain.Successor = decorator;
                lastInChain = decorator;
            }
        }

        private IHandleRequests<TRequest> PushOntoChain(IEnumerable<RequestHandlerAttribute> attributes, IHandleRequests<TRequest> lastInChain)
        {
            foreach (var attribute in attributes) 
            {
                var decorator = new HandlerFactory<TRequest>(attribute, _container).CreateRequestHandler();
                decorator.Successor = lastInChain;
                lastInChain = decorator;
            }
            return lastInChain;
        }
    }
}