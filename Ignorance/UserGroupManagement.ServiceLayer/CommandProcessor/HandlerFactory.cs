namespace UserGroupManagement.ServiceLayer.CommandProcessor
{
    using System;
    using System.Linq;

    using Castle.Windsor;

    using UserGroupManagement.ServiceLayer.CommandHandlers;
    using UserGroupManagement.ServiceLayer.Common;

    internal class HandlerFactory<TRequest> where TRequest : class, IRequest
    {
        private readonly RequestHandlerAttribute _attribute;
        private readonly IWindsorContainer _container;
        private readonly Type _messageType;

        public HandlerFactory(RequestHandlerAttribute attribute, IWindsorContainer container)
        {
            _attribute = attribute;
            _container = container;
            _messageType = typeof(TRequest);
        }

        public IHandleRequests<TRequest> CreateRequestHandler()
        {
            var handlerType = _attribute.GetHandlerType().MakeGenericType(_messageType);
            var parameters = handlerType.GetConstructors()[0].GetParameters().Select(param => _container.Resolve(param.ParameterType)).ToArray();
            return (IHandleRequests<TRequest>)Activator.CreateInstance(handlerType, parameters);
        }
    }
}