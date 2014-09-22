using System.Collections.Generic;
using paramore.brighter.restms.server.Ports.Common;

namespace paramore.brighter.restms.server.Adapters.Repositories
{
    public class InMemoryRepository<T> : IAmARepository<T> where T: class, IAmAnAggregate
    {
        readonly Dictionary<Identity, T> domains = new  Dictionary<Identity, T>();
        public void Add(T aggregate)
        {
            domains.Add(aggregate.Id, aggregate);
        }

        public T this[Identity index]
        {
            get
            {
                if (domains.ContainsKey(index))
                    return domains[index];
                else
                    return null;
            }
        }
    }
}