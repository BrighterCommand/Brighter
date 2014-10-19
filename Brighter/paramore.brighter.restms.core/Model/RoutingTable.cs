using System.Collections.Generic;

namespace paramore.brighter.restms.core.Model
{
    public class RoutingTable
    {
        readonly Dictionary<Address, List<Join>> joins = new Dictionary<Address, List<Join>>();

        public IEnumerable<Join> this[Address address]
        {
            get
            {
                List<Join> value;
                if (joins.TryGetValue(address, out value))
                {
                    return value;
                }
                else
                {
                    return CreateEmptyJoinList();
                }
            }
            set
            {
                List<Join> matchingJoins;
                if (!joins.TryGetValue(address, out matchingJoins))
                {
                    matchingJoins = new List<Join>();
                    joins[address] = matchingJoins;
                }

                matchingJoins.AddRange(value);
            }
        }

        List<Join> CreateEmptyJoinList()
        {
            return new List<Join>();
        }
    }
}
