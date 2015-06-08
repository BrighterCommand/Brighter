using System;
using System.Collections.Generic;
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
            
            
            throw new NotImplementedException();
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