using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal sealed class BrokeredMessageWrapper : IBrokeredMessageWrapper
    {
        private readonly ServiceBusReceivedMessage _brokeredMessage;

        public BrokeredMessageWrapper(ServiceBusReceivedMessage brokeredMessage)
        {
            _brokeredMessage = brokeredMessage;
        }

        public byte[] MessageBodyValue => _brokeredMessage.Body.ToArray();

        public IReadOnlyDictionary<string, object> ApplicationProperties => _brokeredMessage.ApplicationProperties;

        public string LockToken => _brokeredMessage.LockToken;

        public string Id => _brokeredMessage.MessageId;

        public string CorrelationId
        {
            get
            {
                return string.IsNullOrEmpty(_brokeredMessage.CorrelationId)
                    ? string.Empty
                    : _brokeredMessage.CorrelationId;
            }
        }

        public string ContentType => _brokeredMessage.ContentType;
    }
}
