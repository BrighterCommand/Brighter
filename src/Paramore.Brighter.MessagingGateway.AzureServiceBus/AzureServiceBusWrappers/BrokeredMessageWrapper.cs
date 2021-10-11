using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal class BrokeredMessageWrapper : IBrokeredMessageWrapper
    {
        private readonly ServiceBusReceivedMessage _brokeredMessage;

        public BrokeredMessageWrapper(ServiceBusReceivedMessage brokeredMessage)
        {
            _brokeredMessage = brokeredMessage;
        }

        public byte[] MessageBodyValue => _brokeredMessage.Body.ToArray();

        public IReadOnlyDictionary<string, object> ApplicationProperties => _brokeredMessage.ApplicationProperties;

        public string LockToken => _brokeredMessage.LockToken;

        public Guid Id
        {
            get
            {
                return Guid.Parse(_brokeredMessage.MessageId);
            }
        }

        public Guid CorrelationId
        {
            get
            {
                return string.IsNullOrEmpty(_brokeredMessage.CorrelationId)
                    ? Guid.Empty
                    : Guid.Parse(_brokeredMessage.CorrelationId);
            }
        }

        public string ContentType { get => _brokeredMessage.ContentType; }
    }
}
