namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusPublication : Publication
    {
        //TODO: Placeholder for producer specific properties if required
        
        /// <summary>
        /// Use a Service Bus Queue instead of a Topic
        /// </summary>
        public bool UseServiceBusQueue = false;
    }
}
