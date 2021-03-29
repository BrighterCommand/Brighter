using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class MessageReceiverWrapper : IMessageReceiverWrapper
    {
        private readonly IMessageReceiver _messageReceiver;
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MessageReceiverWrapper>);

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
            Logger.Value.Warn("Closing the MessageReceiver connection");
            _messageReceiver.CloseAsync().GetAwaiter().GetResult();
            Logger.Value.Warn("MessageReceiver connection stopped");
        }

        public async Task Complete(string lockToken)
        {
            await _messageReceiver.CompleteAsync(lockToken).ConfigureAwait(false);
        }

        public bool IsClosedOrClosing => _messageReceiver.IsClosedOrClosing;
    }
}
