using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Windsor;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.Common;

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

        IHandleRequests<TRequest> GetHandler()
        {
             
            var handlers = new RequestHandlers<TRequest>(_container.ResolveAll(_implicithandlerType));

            handlers.CheckForMissingHandler();

            return handlers.First();
        }

        private IHandleRequests<TRequest> BuildChain(IHandleRequests<TRequest> implicitHandler)
        {
            var preAttributes = GetOtherHandlersInChain(FindHandlerMethod(implicitHandler)).Where(attribute => attribute.Timing == HandlerTiming.Before).OrderByDescending(attribute => attribute.Step); 
            var firstInChain = PushOntoChain(preAttributes, implicitHandler);
            var postAttributes = GetOtherHandlersInChain(FindHandlerMethod(implicitHandler)).Where(attribute => attribute.Timing == HandlerTiming.After).OrderByDescending(attribute => attribute.Step);
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


        //REFACTOR: no this access, methodinfo needs a wrapper that has this behavior; could be an extenstionmethod

        private IEnumerable<RequestHandlerAttribute> GetOtherHandlersInChain(MethodInfo targetMethod)
        {
            var customAttributes = targetMethod.GetCustomAttributes(true);
            return customAttributes
                     .Select(attr => (Attribute)attr)
                     .Cast<RequestHandlerAttribute>()
                     .Where(a => a.GetType().BaseType == typeof(RequestHandlerAttribute))
                     .ToList();
        }

        //REFACTOR: no this access, targethandler needs a wrapper for this behavior or extension method 

        private MethodInfo FindHandlerMethod(IHandleRequests<TRequest> targetHandler)
        {
            var methods = targetHandler.GetType().GetMethods();
            return methods
                .Where(method => method.Name == "Handle")
                .Where(method => method.GetParameters().Count() == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest))
                .SingleOrDefault();
        }
    }
}