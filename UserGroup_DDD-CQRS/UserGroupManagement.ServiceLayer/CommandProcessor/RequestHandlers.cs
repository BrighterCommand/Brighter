using System;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.CommandProcessor
{

    internal class RequestHandlers<TRequest> where TRequest : class, IRequest
    {
        private readonly Array _handlers;

        internal RequestHandlers(Array handlers)
        {
            _handlers = handlers;
        }

        internal void CheckForMissingHandler()
        {
            if (this._handlers.Length == 0)
            {
                throw new ArgumentOutOfRangeException(string.Format("No implicit handler found for command: {0}", typeof(TRequest)));
            }
        }

        internal IHandleRequests<TRequest> First()
        {
            return (IHandleRequests<TRequest>)_handlers.GetValue(0);
        }
    }
}