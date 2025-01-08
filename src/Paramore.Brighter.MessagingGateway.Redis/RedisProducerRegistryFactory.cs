using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisProducerRegistryFactory(
        RedisMessagingGatewayConfiguration redisConfiguration,
        IEnumerable<RedisMessagePublication> publications)
        : IAmAProducerRegistryFactory
    {
        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
        public IAmAProducerRegistry Create()
        {
            var producerFactory = new RedisMessageProducerFactory(redisConfiguration, publications);

            return new ProducerRegistry(producerFactory.Create());
        }

        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
        public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Create());
        }
    }
}
