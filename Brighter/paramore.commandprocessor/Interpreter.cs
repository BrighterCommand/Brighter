using System;
using System.Collections.Generic;

namespace paramore.brighter.commandprocessor
{
    internal class Interpreter<TRequest> where TRequest : class, IRequest
    {
        private readonly IAdaptAnInversionOfControlContainer container;

        public Interpreter(IAdaptAnInversionOfControlContainer container)
        {
            this.container = container ;
        }

        public IEnumerable<RequestHandler<TRequest>> GetHandlers(Type requestType)
        {
            var handlerGenericType = typeof(IHandleRequests<>);
            var implicithandlerType = handlerGenericType.MakeGenericType(typeof(TRequest));

            var handlers = new RequestHandlers<TRequest>(container.GetAllInstances(implicithandlerType));
            return handlers;
        }
    }
}