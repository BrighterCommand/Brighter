using System.Collections;
using System.Collections.Generic;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.CommandProcessor
{

    public class Chains<TRequest> : IEnumerable<IHandleRequests<TRequest>> where TRequest : class, IRequest
    {
        private readonly List<IHandleRequests<TRequest>> _chains = new List<IHandleRequests<TRequest>>();

        public IEnumerator<IHandleRequests<TRequest>> GetEnumerator()
        {
            return _chains.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IHandleRequests<TRequest> handler)
        {
            _chains.Add(handler);
        }
    }
}
