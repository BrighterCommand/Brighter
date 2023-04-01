using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal class ServiceBusReceiverWrapper : IServiceBusReceiverWrapper
    {
        private readonly ServiceBusReceiver _messageReceiver;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ServiceBusReceiverWrapper>();

        public ServiceBusReceiverWrapper(ServiceBusReceiver messageReceiver)
        {
            _messageReceiver = messageReceiver;
        }

        public async Task<IEnumerable<IBrokeredMessageWrapper>> Receive(int batchSize, TimeSpan serverWaitTime)
        {
            var messages = await _messageReceiver.ReceiveMessagesAsync(batchSize, serverWaitTime).ConfigureAwait(false);

            if (messages == null)
            {
                return new List<IBrokeredMessageWrapper>();
            }
            return messages.Select(x => new BrokeredMessageWrapper(x));
        }

        public void Close()
        {
            s_logger.LogWarning("Closing the MessageReceiver connection");
            _messageReceiver.CloseAsync().GetAwaiter().GetResult();
            s_logger.LogWarning("MessageReceiver connection stopped");
        }

        public async Task Complete(string lockToken)
        {
            await _messageReceiver.CompleteMessageAsync(CreateMessageShiv(lockToken)).ConfigureAwait(false);
        }

        public async Task DeadLetter(string lockToken)
        {
            await _messageReceiver.DeadLetterMessageAsync(CreateMessageShiv(lockToken));
        }

        public bool IsClosedOrClosing => _messageReceiver.IsClosed;


        private ServiceBusReceivedMessage CreateMessageShiv(string lockToken)
        {
            return ServiceBusModelFactory.ServiceBusReceivedMessage(lockTokenGuid: Guid.Parse(lockToken));
        }
    }
}
