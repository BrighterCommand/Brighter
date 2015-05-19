using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreViewerModelRetriever : IMessageStoreViewerModelRetriever
    {
        private MessageStoreViewerModel _fakeResult;
        private MessageStoreViewerModelError fakeError;

        public FakeMessageStoreViewerModelRetriever(MessageStoreViewerModel messageStoreViewerModel)
        {
            _fakeResult = messageStoreViewerModel;
        }

        public FakeMessageStoreViewerModelRetriever(MessageStoreViewerModelError error)
        {
            fakeError = error;
        }

        private FakeMessageStoreViewerModelRetriever()
        {
        }

        public ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> Get(string storeName)
        {
            if (_fakeResult != null)
            {
                return
                    new ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError>(_fakeResult);
            }
            return new ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError>(fakeError);
        }

        public static IMessageStoreViewerModelRetriever Empty()
        {
            return new FakeMessageStoreViewerModelRetriever();
        }
    }
}