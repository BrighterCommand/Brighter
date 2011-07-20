using System;
using System.Collections.Generic;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.CommandProcessor
{
    using System.Collections;
    using System.Linq;

    internal class RequestHandlers<TRequest> : IEnumerable<RequestHandler<TRequest>>
        where TRequest : class, IRequest
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

        internal RequestHandler<TRequest> First()
        {
            return (RequestHandler<TRequest>)_handlers.GetValue(0);
        }

        public IEnumerator<RequestHandler<TRequest>> GetEnumerator()
        {
            return _handlers.Cast<RequestHandler<TRequest>>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}