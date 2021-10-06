namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusConfiguration
    {
        public AzureServiceBusConfiguration(string connectionString, bool ackOnRead = false )
        {
            ConnectionString = connectionString;
            AckOnRead = ackOnRead;
        }

        public string ConnectionString { get; }

        /// <summary>
        /// When set to true this will Change ReceiveMode from ReceiveAndDelete to PeekAndLock
        /// </summary>
        public bool AckOnRead{ get; }
    }
}
