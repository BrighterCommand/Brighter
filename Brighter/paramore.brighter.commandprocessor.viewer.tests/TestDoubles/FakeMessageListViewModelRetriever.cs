using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageListViewModelRetriever : IMessageListViewModelRetriever
    {
        private readonly MessageListModel _fakeResultModel;
        private MessageListModelError _fakeResultError;

        public FakeMessageListViewModelRetriever(MessageListModel fakeResultModel)
        {
            _fakeResultModel = fakeResultModel;
        }

        public FakeMessageListViewModelRetriever(MessageListModelError fakeResultError)
        {
            _fakeResultError = fakeResultError;
        }

        private FakeMessageListViewModelRetriever()
        {
        }

        public ViewModelRetrieverResult<MessageListModel, MessageListModelError> Get(string storeName, int pageSize, int pageNumber)
        {
            if (_fakeResultModel != null)
            {
                return new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(_fakeResultModel);
            }
            else
            {
                return new ViewModelRetrieverResult<MessageListModel, MessageListModelError>(_fakeResultError);
                
            }
        }

        public ViewModelRetrieverResult<MessageListModel, MessageListModelError> Filter(string messageStoreName, string searchTerm)
        {
            return null;
        }

        public static IMessageListViewModelRetriever Empty()
        {
            return  new FakeMessageListViewModelRetriever();
        }
    }
}