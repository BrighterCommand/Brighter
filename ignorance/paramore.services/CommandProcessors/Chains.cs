using System.Collections;
using System.Collections.Generic;
using Paramore.Services.CommandHandlers;
using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessors
{

    public class Chains<TRequest> : IEnumerable<IHandleRequests<TRequest>> where TRequest : class, IRequest
    {
        private readonly List<IHandleRequests<TRequest>> chains = new List<IHandleRequests<TRequest>>();

        public IEnumerator<IHandleRequests<TRequest>> GetEnumerator()
        {
            return chains.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IHandleRequests<TRequest> handler)
        {
            chains.Add(handler);
        }
    }
}
