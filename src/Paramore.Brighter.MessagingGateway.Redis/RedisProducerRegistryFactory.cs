using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly RedisMessagingGatewayConfiguration _redisConfiguration;
        private readonly IEnumerable<RedisMessagePublication> _publications;

        public RedisProducerRegistryFactory(
            RedisMessagingGatewayConfiguration redisConfiguration, 
            IEnumerable<RedisMessagePublication> publications)
        {
            _redisConfiguration = redisConfiguration;
            _publications = publications;
        }
        
        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
        public IAmAProducerRegistry Create()
        {
            var producerFactory = new RedisMessageProducerFactory(_redisConfiguration, _publications);

            return new ProducerRegistry(producerFactory.Create());
        }
    }
}
