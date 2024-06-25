using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Creates a message producer registry, which contains a producer for every publication
    /// keyed by the topic (routing key)
    /// </summary>
    public class RmqProducerRegistryFactory(
        RmqMessagingGatewayConnection connection,
        IEnumerable<RmqPublication> publications)
        : IAmAProducerRegistryFactory
    {
        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
        public IAmAProducerRegistry Create()
        {
            var producers = new Dictionary<string, IAmAMessageProducer>();
            foreach (var publication in publications)
            {
                producers[publication.Topic] = new RmqMessageProducer(connection, publication);
            }

            return new ProducerRegistry(producers);
        }
    }
}
