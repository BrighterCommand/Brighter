using System;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using IServiceBusClientProvider = Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider.IServiceBusClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Factory class for creating instances of <see cref="AzureServiceBusConsumer"/>
    /// </summary>
    public class AzureServiceBusConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly IServiceBusClientProvider _clientProvider;

        /// <summary>
        /// Factory to create an Azure Service Bus Consumer
        /// </summary>
        /// <param name="configuration">The configuration to connect to <see cref="AzureServiceBusConfiguration"/></param>
        public AzureServiceBusConsumerFactory(AzureServiceBusConfiguration configuration)
            : this(new ServiceBusConnectionStringClientProvider(configuration.ConnectionString))
        { }

        /// <summary>
        /// Factory to create an Azure Service Bus Consumer
        /// </summary>
        /// <param name="clientProvider">A client Provider <see cref="IServiceBusClientProvider"/> to determine how to connect to ASB</param>
        public AzureServiceBusConsumerFactory(IServiceBusClientProvider clientProvider)
        {
            _clientProvider = clientProvider;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync</returns>
        public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider);

            if (!(subscription is AzureServiceBusSubscription sub))
                throw new ArgumentException("Subscription is not of type AzureServiceBusSubscription.",
                    nameof(subscription));

            var receiverProvider = new ServiceBusReceiverProvider(_clientProvider);

            if (sub.Configuration.UseServiceBusQueue)
            {
                var messageProducer = new AzureServiceBusQueueMessageProducer(
                    nameSpaceManagerWrapper,
                    new ServiceBusSenderProvider(_clientProvider),
                    new AzureServiceBusPublication { MakeChannels = subscription.MakeChannels });

                return new AzureServiceBusQueueConsumer(
                    sub,
                    messageProducer,
                    nameSpaceManagerWrapper,
                    receiverProvider);
            }
            else
            {
                var messageProducer = new AzureServiceBusTopicMessageProducer(
                    nameSpaceManagerWrapper,
                    new ServiceBusSenderProvider(_clientProvider),
                    new AzureServiceBusPublication { MakeChannels = subscription.MakeChannels });

                return new AzureServiceBusTopicConsumer(
                    sub,
                    messageProducer,
                    nameSpaceManagerWrapper,
                    receiverProvider);
            }
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync</returns>
        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            var consumer = Create(subscription) as IAmAMessageConsumerAsync;   
            if (consumer == null)
                throw new ChannelFailureException("AzureServiceBusConsumerFactory: Failed to create an async consumer");
            return consumer;
        }
    }
}
