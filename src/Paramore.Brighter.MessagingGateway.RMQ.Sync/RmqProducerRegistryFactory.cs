using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
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
            var producerFactory = new RmqMessageProducerFactory(connection, publications);

            return new ProducerRegistry(producerFactory.Create());
        }

        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <remarks>Not implemented in this package. This package supports only RMQ.Client V6 which is blocking, use the Paramore.Brighter.MessagingGateway.RMQ.Async for async clients</remarks>
        /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
        public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
