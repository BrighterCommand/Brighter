using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class BrokeredMessageWrapper : IBrokeredMessageWrapper
    {
        private readonly Microsoft.Azure.ServiceBus.Message _brokeredMessage;

        public BrokeredMessageWrapper(Microsoft.Azure.ServiceBus.Message brokeredMessage)
        {
            _brokeredMessage = brokeredMessage;
        }

        public byte[] MessageBodyValue => _brokeredMessage.Body;
        
        public IDictionary<string, object> UserProperties => _brokeredMessage.UserProperties;

        public string LockToken => _brokeredMessage.SystemProperties.LockToken;
    }
}
