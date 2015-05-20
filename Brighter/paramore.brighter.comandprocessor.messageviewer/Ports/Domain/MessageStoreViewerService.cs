using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Domain
{
    public interface IMessageStoreViewerService
    {
        IAmAMessageStoreViewer<Message> GetStoreViewer(string storeName, out ViewModelRetrieverResult<MessageListModel, MessageListModelError> errorResult);
    }

    public class MessageStoreViewerService : IMessageStoreViewerService
    {
        private readonly IMessageStoreViewerFactory _messageStoreViewerFactory;

        public MessageStoreViewerService(IMessageStoreViewerFactory messageStoreViewerFactory)
        {
            _messageStoreViewerFactory = messageStoreViewerFactory;
        }

        public IAmAMessageStoreViewer<Message> GetStoreViewer(string storeName, out ViewModelRetrieverResult<MessageListModel, MessageListModelError> errorResult)
        {
            IAmAMessageStore<Message> foundStore = _messageStoreViewerFactory.Connect(storeName);
            if (foundStore == null)
            {
                {
                    errorResult = new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(
                        MessageListModelError.StoreNotFound);
                    return null;
                }
            }
            var foundViewer = foundStore as IAmAMessageStoreViewer<Message>;
            if (foundViewer == null)
            {
                {
                    errorResult = new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(
                        MessageListModelError.StoreMessageViewerNotImplemented);
                    return null;
                }
            }
            errorResult = null;
            return foundViewer;
        }
    }
}