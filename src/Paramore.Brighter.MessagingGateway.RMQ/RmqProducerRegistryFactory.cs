﻿using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Creates a message producer registry, which contains a producer for every publication
    /// keyed by the topic (routing key)
    /// </summary>
    public class RmqProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly RmqMessagingGatewayConnection _connection;
        private readonly IEnumerable<RmqPublication> _publications;

        public RmqProducerRegistryFactory(
            RmqMessagingGatewayConnection connection,
            IEnumerable<RmqPublication> publications)
        {
            _connection = connection;
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
                producers[publication.Topic] = new RmqMessageProducer(_connection, publication);
            }

            return new ProducerRegistry(producers);
        }
    }
}
