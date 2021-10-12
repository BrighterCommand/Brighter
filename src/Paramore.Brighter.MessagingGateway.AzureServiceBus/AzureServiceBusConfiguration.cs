namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Configuration Options for the Azure Service Bus Messaging Transport.
    /// </summary>
    public class AzureServiceBusConfiguration
    {
        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusConfiguration"/>
        /// </summary>
        /// <param name="connectionString">The Connection String to connect to Azure Service Bus.</param>
        /// <param name="ackOnRead">If True Messages and Read a Deleted, if False Messages are Peeked and Locked.</param>
        public AzureServiceBusConfiguration(string connectionString, bool ackOnRead = false )
        {
            ConnectionString = connectionString;
            AckOnRead = ackOnRead;
        }

        /// <summary>
        /// The Connection String to connect to Azure Service Bus.
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// When set to true this will set the Channel to Read and Delete, when False Peek and Lock
        /// </summary>
        public bool AckOnRead{ get; }
    }
}
