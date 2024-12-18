using System.Collections.Generic;

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
    }
}
