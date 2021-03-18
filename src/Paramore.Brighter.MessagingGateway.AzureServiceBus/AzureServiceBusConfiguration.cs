namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusConfiguration
    {
        public AzureServiceBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }
    }
}
