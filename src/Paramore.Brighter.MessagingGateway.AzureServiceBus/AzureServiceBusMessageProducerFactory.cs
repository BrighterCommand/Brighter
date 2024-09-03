using System;
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
        public Dictionary<RoutingKey, IAmAMessageProducer> Create()
        {
            var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider);
            var topicClientProvider = new ServiceBusSenderProvider(_clientProvider);

            var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
            foreach (var publication in _publications)
            {
                if (publication.Topic is null)
                    throw new ArgumentException("Publication must have a Topic.");
                if(publication.UseServiceBusQueue)
                    producers.Add(publication.Topic, new AzureServiceBusQueueMessageProducer(nameSpaceManagerWrapper, topicClientProvider, publication, _bulkSendBatchSize));
                else
                    producers.Add(publication.Topic, new AzureServiceBusTopicMessageProducer(nameSpaceManagerWrapper, topicClientProvider, publication, _bulkSendBatchSize));
            }

            return producers;
        }        
    }
}
