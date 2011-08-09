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
        private readonly IEnumerable<object> handlers;

        internal RequestHandlers(IEnumerable<object> handlers)
        {
            this.handlers = handlers;
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