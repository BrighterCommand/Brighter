namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Interface for a provider of the ServiceBusSender
    /// </summary>
    public interface IServiceBusSenderProvider
    {
        /// <summary>
        /// Get a ServiceBusSenderWrapper for a Topic.
        /// </summary>
        /// <param name="topic">The name of the Topic.</param>
        /// <returns>A ServiceBusSenderWrapper for the given Topic.</returns>
        IServiceBusSenderWrapper Get(string topic);
    }
}
