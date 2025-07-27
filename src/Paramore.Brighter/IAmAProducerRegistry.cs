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
        /// <param name="requestType">The <see cref="CloudEventsType"/> of the expected message, may be null</param>
        /// <param name="requestContext">The <see cref="RequestContext"/> whose Destination property can override routing</param>
        /// <returns>A <see cref="IAmAMessageProducer"/> instance</returns>
        IAmAMessageProducer LookupBy(RoutingKey topic, CloudEventsType? requestType = null, RequestContext? requestContext = null);

        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <param name="requestType">The <see cref="CloudEventsType"/> of the expected message, may be null</param>
        /// <returns>A <see cref="IAmAMessageProducerAsync"/> instance</returns>
        IAmAMessageProducerAsync  LookupAsyncBy(RoutingKey topic, CloudEventsType? requestType = null);
        
        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <param name="requestType">The <see cref="CloudEventsType"/> of the expected message, may be null</param>
        /// <returns>A producer</returns>
        IAmAMessageProducerSync LookupSyncBy(RoutingKey topic, CloudEventsType? requestType = null);
        
        /// <summary>
        /// An iterable list of all the producers in the registry
        /// </summary>
        IEnumerable<IAmAMessageProducer> Producers { get; }

    }
}
