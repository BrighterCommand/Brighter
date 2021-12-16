using System.Collections.Generic;
using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SnsProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly IEnumerable<SnsPublication> _snsPublications;

        /// <summary>
        /// Create a collection of producers from the publication information
        /// </summary>
        /// <param name="connection">The Connection to use to connect to AWS</param>
        /// <param name="snsPublications">The publication describing the SNS topic that we want to use</param>
        public SnsProducerRegistryFactory(
            AWSMessagingGatewayConnection connection,
            IEnumerable<SnsPublication> snsPublications)
        {
            _connection = connection;
            _snsPublications = snsPublications;
        }

        /// <summary>
        /// Create a message producer for each publication, add it into the registry under the key of the topic
        /// </summary>
        /// <returns></returns>
        public IAmAProducerRegistry Create()
        {
            var producers = new Dictionary<string, IAmAMessageProducer>();
            foreach (var p in _snsPublications)
            {
                producers[p.Topic] = new SqsMessageProducer(_connection, p);
            }

            return new ProducerRegistry(producers);
        }
    }
}
