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
        private readonly Array handlers;

        internal RequestHandlers(Array handlers)
        {
            this.handlers = handlers;
        }

        internal RequestHandler<TRequest> First()
        {
            return (RequestHandler<TRequest>)handlers.GetValue(0);
        }

        public IEnumerator<RequestHandler<TRequest>> GetEnumerator()
        {
            return handlers.Cast<RequestHandler<TRequest>>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}