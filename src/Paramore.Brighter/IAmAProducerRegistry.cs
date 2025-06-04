using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter
{
    ///A list of producers by topic. We look up the producer via the topic in the message header when sending
    public interface IAmAProducerRegistry : IDisposable
    {
        /// <summary>
        /// Iterates through all the producers and disposes them, as they may have unmanaged resources that should be shut down in an orderly fashion
        /// </summary>
        void CloseAll();
        
        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A <see cref="IAmAMessageProducer"/> instance</returns>
        IAmAMessageProducer LookupBy(RoutingKey topic);
        
        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A <see cref="IAmAMessageProducerAsync"/> instance</returns>
        IAmAMessageProducerAsync  LookupAsyncBy(RoutingKey topic);

        /// <summary>
        /// An iterable list of all the producers in the registry
        /// </summary>
        IEnumerable<IAmAMessageProducer> Producers { get; }

        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A producer</returns>
        IAmAMessageProducerSync LookupSyncBy(RoutingKey topic);
    }
}
