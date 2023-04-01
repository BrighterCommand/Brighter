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
            var producers = new Dictionary<string, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                producers[publication.Topic] = new RedisMessageProducer(_redisConfiguration, publication);
            }

            return new ProducerRegistry(producers);
        }
    }
}
