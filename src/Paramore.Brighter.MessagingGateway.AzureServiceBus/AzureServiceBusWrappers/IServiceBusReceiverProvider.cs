using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Interface for a Provider to provide <see cref="IServiceBusReceiverWrapper"/>
    /// </summary>
    public interface IServiceBusReceiverProvider
    {
        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/> for a Service Bus Queue
        /// </summary>
        /// <param name="queueName">The name of the Topic.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
        Task<IServiceBusReceiverWrapper?> GetAsync(string queueName, bool sessionEnabled);

        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/> for a Service Bus Topic
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription on the Topic.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <param name="ackOnRead">When set to True this will remove the message from the channel when it is read.</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
        Task<IServiceBusReceiverWrapper?> GetAsync(string topicName, string subscriptionName, bool sessionEnabled, bool ackOnRead = false);
    }
}
