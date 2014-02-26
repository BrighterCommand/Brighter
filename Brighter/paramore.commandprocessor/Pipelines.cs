using System.Collections;
using System.Collections.Generic;

namespace paramore.commandprocessor
{
    public class Pipelines<TRequest> : IEnumerable<IHandleRequests<TRequest>> where TRequest : class, IRequest
    {
        private readonly List<IHandleRequests<TRequest>> filters = new List<IHandleRequests<TRequest>>();

        public IEnumerator<IHandleRequests<TRequest>> GetEnumerator()
        {
            return filters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IHandleRequests<TRequest> handler)
        {
            filters.Add(handler);
        }
    }
}
