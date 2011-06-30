using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public ChainofResponsibilityBuilder(IWindsorContainer container)
        {
            _container = container;
        }

        public IHandleRequests<TRequest> Build()
        {
            return BuildChain(GetHandler());
        }

        IHandleRequests<TRequest> GetHandler()
        {
            var handlers = _container.ResolveAll(GetHandlerType());

            CheckForMissingHandler(handlers);

            return (IHandleRequests<TRequest>)handlers.GetValue(0);
        }

        private void CheckForMissingHandler(Array handlers)
        {
            if (handlers.Length == 0)
            {
                throw new ArgumentOutOfRangeException(string.Format("No implicit handler found for command: {0}", typeof(TRequest)));
            }
        }

        Type GetHandlerType()
        {
            var messageType = typeof(TRequest);
            var handlerGenericType = typeof(IHandleRequests<>);
            return handlerGenericType.MakeGenericType(messageType);
        }

        private  IHandleRequests<TRequest> BuildChain(IHandleRequests<TRequest> implicitHandler)
        {
            var lastInChain = implicitHandler;
            var attributes = GetOtherHandlersInChain(FindHandlerMethod(implicitHandler)).OrderByDescending(attribute => attribute.Step); 
            foreach (var attribute in attributes) 
            {
                IHandleRequests<TRequest> decorator = CreateRequestHandler(attribute);
                decorator.Successor = lastInChain;
                lastInChain = decorator;
            }
            return lastInChain;
        }

        private IHandleRequests<TRequest> CreateRequestHandler(RequestHandlerAttribute attribute)
        {
            var handlerType = attribute.GetHandlerType().MakeGenericType(typeof(TRequest));
            var parameters = handlerType.GetConstructors()[0].GetParameters().Select(param => _container.Resolve(param.ParameterType)).ToArray();
            return (IHandleRequests<TRequest>)Activator.CreateInstance(handlerType, parameters);
        }

        private IEnumerable<RequestHandlerAttribute> GetOtherHandlersInChain(MethodInfo targetMethod)
        {
            var customAttributes = targetMethod.GetCustomAttributes(true);
            return customAttributes
                     .Select(attr => (Attribute)attr)
                     .Cast<RequestHandlerAttribute>()
                     .Where(a => a.GetType().BaseType == typeof(RequestHandlerAttribute))
                     .ToList();
        }

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