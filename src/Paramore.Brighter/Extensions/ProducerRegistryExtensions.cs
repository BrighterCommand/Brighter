using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Extensions
{
    public static class ProducerRegistryExtensions
    {
        public static IAmAProducerRegistry Merge(this IAmAProducerRegistry lhs, IAmAProducerRegistry rhs)
        {
            if (rhs == null)
                return lhs;
            
            if (lhs == null)
                return rhs;
            
            var producers = 
                lhs
                .KeyedProducers.Concat(rhs.KeyedProducers)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            return new ProducerRegistry(producers);
        }
    }
}
