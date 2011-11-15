using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace paramore.commandprocessor
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