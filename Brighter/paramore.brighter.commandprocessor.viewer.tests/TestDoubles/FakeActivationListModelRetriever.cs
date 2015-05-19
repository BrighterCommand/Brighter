using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeActivationListModelRetriever : IMessageStoreActivationStateListViewModelRetriever
    {
        private MessageStoreActivationStateListModel _fakeResult = null;
        private MessageStoreActivationStateListModelError _fakeError;

        public FakeActivationListModelRetriever(MessageStoreActivationStateListModel storeListModel)
        {
            _fakeResult = storeListModel;
        }

        private FakeActivationListModelRetriever()
        {
        }

        public FakeActivationListModelRetriever(MessageStoreActivationStateListModelError error)
        {
            _fakeError = error;
        }

        public ViewModelRetrieverResult<MessageStoreActivationStateListModel, MessageStoreActivationStateListModelError> Get()
        {
            if (_fakeResult != null)
            {
                return
                    new ViewModelRetrieverResult<MessageStoreActivationStateListModel, MessageStoreActivationStateListModelError>(
                        _fakeResult);
            }
            return new ViewModelRetrieverResult<MessageStoreActivationStateListModel, MessageStoreActivationStateListModelError>(_fakeError);
        }

        public static IMessageStoreActivationStateListViewModelRetriever Empty()
        {
            return new FakeActivationListModelRetriever();
        }
    }
}