using System;
using System.Linq;
using TinyIoC;

namespace paramore.commandprocessor
{
    internal class HandlerFactory<TRequest> where TRequest : class, IRequest
    {
        private readonly RequestHandlerAttribute attribute;
        private readonly TinyIoCContainer container;
        private readonly Type messageType;

        public HandlerFactory(RequestHandlerAttribute attribute, TinyIoCContainer container)
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