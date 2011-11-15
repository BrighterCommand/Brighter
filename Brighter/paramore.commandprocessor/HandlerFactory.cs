using System;
using System.Linq;
using paramore.commandprocessor.sharedinterfaces;

namespace paramore.commandprocessor
{
    internal class HandlerFactory<TRequest> where TRequest : class, IRequest
    {
        private readonly RequestHandlerAttribute attribute;
        private readonly IAmAnInversionOfControlContainer container;
        private readonly Type messageType;

        public HandlerFactory(RequestHandlerAttribute attribute, IAmAnInversionOfControlContainer  container)
        {
            this.attribute = attribute;
            this.container = container;
            messageType = typeof(TRequest);
        }

        public IHandleRequests<TRequest> CreateRequestHandler()
        {
            var handlerType = attribute.GetHandlerType().MakeGenericType(messageType);
            var parameters = handlerType.GetConstructors()[0].GetParameters().Select(param => container.Resolve(param.ParameterType)).ToArray();
            return (IHandleRequests<TRequest>)Activator.CreateInstance(handlerType, parameters);
        }
    }
}