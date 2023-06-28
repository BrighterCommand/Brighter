using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Interface for a Provider to provide <see cref="IServiceBusReceiverWrapper"/>
    /// </summary>
    public interface IServiceBusReceiverProvider
    {
        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/>
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription on the Topic.</param>
        /// <param name="receiveMode">The Receive Mode.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
        IServiceBusReceiverWrapper Get(string topicName, string subscriptionName, ServiceBusReceiveMode receiveMode, bool sessionEnabled);
    }
}
