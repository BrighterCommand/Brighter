using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Windsor;
using UserGroupManagement.ServiceLayer.Common;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.CommandProcessor.ReflectionExtensionMethods;

namespace UserGroupManagement.ServiceLayer.CommandProcessor
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

        public IHandleRequests<TRequest> Build()
        {
            return BuildChain(GetHandler());
        }

        RequestHandler<TRequest> GetHandler()
        {
             
            var handlers = new RequestHandlers<TRequest>(_container.ResolveAll(_implicithandlerType));

            handlers.CheckForMissingHandler();

            return (RequestHandler<TRequest>)handlers.First();
        }

        private IHandleRequests<TRequest> BuildChain(RequestHandler<TRequest> implicitHandler)
        {
            var preAttributes = implicitHandler.FindHandlerMethod()
                .GetOtherHandlersInChain()
                .Where(attribute => attribute.Timing == HandlerTiming.Before)
                .OrderByDescending(attribute => attribute.Step); 

            var firstInChain = PushOntoChain(preAttributes, implicitHandler);
            
            var postAttributes = implicitHandler.FindHandlerMethod()
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


        //REFACTOR: no this access, targethandler needs a wrapper for this behavior or extension method 

 
    }
}