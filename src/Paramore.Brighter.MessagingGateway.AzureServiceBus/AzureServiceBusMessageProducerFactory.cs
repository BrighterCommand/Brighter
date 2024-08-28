using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Factory class for creating dictionary of instances of <see cref="AzureServiceBusMessageProducer"/>
    /// indexed by topic name
    /// </summary>
    public class AzureServiceBusMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly IServiceBusClientProvider _clientProvider;
        private readonly IEnumerable<AzureServiceBusPublication> _publications;
        private readonly int _bulkSendBatchSize;

        /// <summary>
        /// Factory to create a dictionary of Azure Service Bus Producers indexed by topic name
        /// </summary>
        /// <param name="configuration">The configuration of the connection to ASB</param>
        /// <param name="publications">A set of publications - topics on the server - to configure</param>
        public AzureServiceBusMessageProducerFactory(
            AzureServiceBusConfiguration configuration,
            IEnumerable<AzureServiceBusPublication> publications)
        {
            _clientProvider = new ServiceBusConnectionStringClientProvider(configuration.ConnectionString);
            _publications = publications;
            _bulkSendBatchSize = configuration.BulkSendBatchSize;
        }

        /// <summary>
        /// Factory to create a dictionary of Azure Service Bus Producers indexed by topic name
        /// </summary>
        /// <param name="clientProvider">The connection to ASB</param>
        /// <param name="publications">A set of publications - topics on the server - to configure</param>
        /// <param name="bulkSendBatchSize">The maximum size to chunk messages when dispatching to ASB</param>
        public AzureServiceBusMessageProducerFactory(
            IServiceBusClientProvider clientProvider,
            IEnumerable<AzureServiceBusPublication> publications,
            int bulkSendBatchSize)
        {
            _clientProvider = clientProvider;
            _publications = publications;
            _bulkSendBatchSize = bulkSendBatchSize;
        }

        /// <inheritdoc />
        public Dictionary<string, IAmAMessageProducer> Create()
        {
            var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider);
            var topicClientProvider = new ServiceBusSenderProvider(_clientProvider);

            var producers = new Dictionary<string, IAmAMessageProducer>();
            foreach (var publication in _publications)
            {
                var producer = new AzureServiceBusMessageProducer(
                    nameSpaceManagerWrapper, 
                    topicClientProvider, 
                    publication, 
                    _bulkSendBatchSize);
                producers.Add(publication.Topic, producer);
            }

            return producers;
        }        
    }
}
