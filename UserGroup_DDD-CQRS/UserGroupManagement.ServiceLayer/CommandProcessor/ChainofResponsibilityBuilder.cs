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
    public class ChainofResponsibilityBuilder<TRequest> where TRequest: class, IRequest
    {
        private readonly IWindsorContainer _container;

        public ChainofResponsibilityBuilder(IWindsorContainer container)
        {
            _container = container;
        }

        public IHandleRequests<TRequest> Build()
        {
            return AddDecorators(GetHandler());
        }

        IHandleRequests<TRequest> GetHandler()
        {
            var handlers = _container.ResolveAll(GetHandlerType());
            if (handlers.Length > 0)
                throw new ArgumentOutOfRangeException(string.Format("More than one implicit handler found for command: {0}", typeof(TRequest)));

            return (IHandleRequests<TRequest>)handlers.GetValue(0);
        }

        //TODO: Static warning is usually a sign that we are missing a class - refactor when tess pass

        [DebuggerStepThrough]
        Type GetHandlerType()
        {
            var messageType = typeof(TRequest);
            var handlerGenericType = typeof(IHandleRequests<>);
            return handlerGenericType.MakeGenericType(messageType);
        }

        private  IHandleRequests<TRequest> AddDecorators(IHandleRequests<TRequest> implicitHandler)
        {
            var lastInChain = implicitHandler;
            var attributeTypes = GetDecoratorAttributesonHandler(FindHandlerMethod(implicitHandler)); 
            attributeTypes.ForEach(attributeType =>
            {
                IHandleRequests<TRequest> decorator = CreateDecorator(attributeType);
                decorator.Successor = lastInChain;
                lastInChain = decorator;
            });
            return lastInChain;
        }

        [DebuggerStepThrough]
        private IHandleRequests<TRequest> CreateDecorator(Attribute attributeType)
        {
            var attribute = (RequestHandlerRequiredDecoratorAttribute)Activator.CreateInstance(attributeType.GetType());
            var decoratorType = attribute.GetDecoratorType().MakeGenericType(typeof (TRequest));
            var parameters = decoratorType.GetConstructors()[0].GetParameters().Select(param => _container.Resolve(param.ParameterType)).ToArray();
            return (IHandleRequests<TRequest>) Activator.CreateInstance(decoratorType, parameters);
        }

        [DebuggerStepThrough]
        private List<Attribute> GetDecoratorAttributesonHandler(MethodInfo targetMethod)
        {
             return targetMethod.GetCustomAttributes(true)
                     .Select(attr => (Attribute)attr)
                     .Where(a => a.GetType().BaseType == typeof(RequestHandlerRequiredDecoratorAttribute))
                     .ToList();
        }

        [DebuggerStepThrough]
        private static MethodInfo FindHandlerMethod(IHandleRequests<TRequest> targetHandler)
        {
            return targetHandler.GetType().GetMethods()
                .Where(method => method.Name == "Handle")
                .Where(method => method.GetParameters().Count() == 1 && method.GetParameters().Single().ParameterType == typeof(TRequest))
                .SingleOrDefault();
        }
    }
}