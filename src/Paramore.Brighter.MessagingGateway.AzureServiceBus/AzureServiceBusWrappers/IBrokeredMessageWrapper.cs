using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IBrokeredMessageWrapper
    {
        byte[] MessageBodyValue { get; }
        
        IDictionary<string, object> UserProperties { get; }   
    }
}
