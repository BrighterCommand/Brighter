using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public class RepostCommand : ICommand
    {
        public List<string> MessageIds { get; set; }
        public string StoreName { get; set; }
    }

    public class RepostCommandHandler : IHandleCommand<RepostCommand>
    {
        private readonly IMessageStoreViewerFactory _messageStoreViewerFactory;

        public RepostCommandHandler(IMessageStoreViewerFactory messageStoreViewerFactory)
        {
            _messageStoreViewerFactory = messageStoreViewerFactory;
        }

        public void Handle(RepostCommand command)
        {
            RepostCommandHandlerError? errorResult;
            var messageStore = GetStore(command.StoreName, out errorResult);
            if (errorResult.HasValue)
            {
                throw new SystemException("Error " + errorResult.Value);
            }

            foreach (var messageId in command.MessageIds)
            {
                var foundMessage = messageStore.Get(Guid.Parse(messageId)).Result;

                var newHeader = new MessageHeader(Guid.NewGuid(), foundMessage.Header.Topic, foundMessage.Header.MessageType);
                foreach (var key in newHeader.Bag.Keys)
                {
                    newHeader.Bag.Add(key, foundMessage.Header.Bag[key]);
                }               
                var newBody = new MessageBody(foundMessage.Body.Value);
                var newMessage = new Message(newHeader, newBody);

                messageStore.Add(newMessage);
            }
        }

        private IAmAMessageStore<Message> GetStore(string storeName, out RepostCommandHandlerError? errorResult)
        {
            IAmAMessageStore<Message> foundStore = _messageStoreViewerFactory.Connect(storeName);
            if (foundStore == null)
            {
                errorResult = RepostCommandHandlerError.StoreNotFound;
                return null;
            }
            errorResult = null;
            return foundStore;
        }

    }

    internal enum RepostCommandHandlerError
    {
        StoreNotFound
    }
}