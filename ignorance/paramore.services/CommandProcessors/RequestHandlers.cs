using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Paramore.Services.CommandHandlers;
using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessors
{
    internal class RequestHandlers<TRequest> : IEnumerable<RequestHandler<TRequest>>
        where TRequest : class, IRequest
    {
        private readonly Array _handlers;

        internal RequestHandlers(Array handlers)
        {
            _handlers = handlers;
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