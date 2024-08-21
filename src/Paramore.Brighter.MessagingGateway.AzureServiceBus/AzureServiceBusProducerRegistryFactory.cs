using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly IServiceBusClientProvider _clientProvider;
        private readonly IEnumerable<AzureServiceBusPublication> _asbPublications;
        private readonly int _bulkSendBatchSize;

        /// <summary>
        /// Creates a producer registry initialized with producers for ASB derived from the publications
        /// </summary>
        /// <param name="configuration">The configuration of the connection to ASB</param>
        /// <param name="asbPublications">A set of publications - topics on the server - to configure</param>
        public AzureServiceBusProducerRegistryFactory(
            AzureServiceBusConfiguration configuration, 
            IEnumerable<AzureServiceBusPublication> asbPublications)
        {
             _clientProvider = new ServiceBusConnectionStringClientProvider(configuration.ConnectionString);
             _asbPublications = asbPublications;
             _bulkSendBatchSize = configuration.BulkSendBatchSize;
        }

        /// <summary>
        /// Creates a producer registry initialized with producers for ASB derived from the publications
        /// </summary>
        /// <param name="clientProvider">The connection to ASB</param>
        /// <param name="asbPublications">A set of publications - topics on the server - to configure</param>
        /// <param name="bulkSendBatchSize">The maximum size to chunk messages when dispatching to ASB</param>
        public AzureServiceBusProducerRegistryFactory(
            IServiceBusClientProvider clientProvider,
            IEnumerable<AzureServiceBusPublication> asbPublications,
            int bulkSendBatchSize = 10)
        {
            _clientProvider = clientProvider;
            _asbPublications = asbPublications;
            _bulkSendBatchSize = bulkSendBatchSize;
        }

        /// <summary>
        /// Creates message producers.
        /// </summary>
        /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
        public IAmAProducerRegistry Create()
        {
            var producerFactory = new AzureServiceBusMessageProducerFactory(_clientProvider, _asbPublications, _bulkSendBatchSize);

            return new ProducerRegistry(producerFactory.Create());
        }
    }
}
