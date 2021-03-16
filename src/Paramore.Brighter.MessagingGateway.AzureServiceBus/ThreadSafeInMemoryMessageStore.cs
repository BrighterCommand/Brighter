using System;
using System.Collections.Concurrent;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class ConcurrentInMemoryMessageStore
    {
        private readonly ConcurrentDictionary<Guid, Message> _pendingMessages = new ConcurrentDictionary<Guid, Message>();

        public void Add(Message message)
        {
            if (!_pendingMessages.TryAdd(message.Id, message))
            {
                if (_pendingMessages.TryGetValue(message.Id, out Message existingMessage) && !MessagesDifferOnlyById(message, existingMessage))
                {
                    throw new ArgumentException($"A different message with the same id has already been added (id={message.Id})");
                }
            }
        }

        public Message Get(Guid messageId)
        {
            _pendingMessages.TryRemove(messageId, out Message message);

            return message;
        }
        
        private static bool MessagesDifferOnlyById(Message a, Message b)
        {
            return a.Header.Topic == b.Header.Topic && a.Header.MessageType == b.Header.MessageType && a.Body == b.Body;
        }
    }
}
