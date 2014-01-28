using System;
using System.Linq;

namespace paramore.commandprocessor
{
    internal class HandlerFactory<TRequest> where TRequest : class, IRequest
    {
        private readonly RequestHandlerAttribute attribute;
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly Type messageType;
        private IRequestContext requestContext;

        public HandlerFactory(RequestHandlerAttribute attribute, IAdaptAnInversionOfControlContainer  container, IRequestContext requestContext)
        {
            this.attribute = attribute;
            this.container = container;
            this.requestContext = requestContext;
            messageType = typeof(TRequest);
        }

        public IHandleRequests<TRequest> CreateRequestHandler()
        {
            var handlerType = attribute.GetHandlerType().MakeGenericType(messageType);
            var parameters = handlerType.GetConstructors()[0].GetParameters().Select(param => container.GetInstance(param.ParameterType)).ToArray();
            var handler = (IHandleRequests<TRequest>)Activator.CreateInstance(handlerType, parameters);
            handler.Context = requestContext;
            return handler;
        }
    }
}