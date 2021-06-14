using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class MessageReceiverWrapper : IMessageReceiverWrapper
    {
        private readonly IMessageReceiver _messageReceiver;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MessageReceiverWrapper>();

        public MessageReceiverWrapper(IMessageReceiver messageReceiver)
        {
            _messageReceiver = messageReceiver;
        }

        public async Task<IEnumerable<IBrokeredMessageWrapper>> Receive(int batchSize, TimeSpan serverWaitTime)
        {
            var messages = await _messageReceiver.ReceiveAsync(batchSize, serverWaitTime).ConfigureAwait(false);

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
            await _messageReceiver.CompleteAsync(lockToken).ConfigureAwait(false);
        }

        public async Task DeadLetter(string lockToken)
        {
            await _messageReceiver.DeadLetterAsync(lockToken);
        }

        public bool IsClosedOrClosing => _messageReceiver.IsClosedOrClosing;
    }
}
