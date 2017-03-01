using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports.MessageListViewModelRetrieverTests
{
    public class MessageListViewModelRetreiverStoreNotInViewerTests
    {
        private MessageListViewModelRetriever _messageListViewModelRetriever;
        private ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        private readonly string _storeName = "storeNotImplementingViewer";

        [SetUp]
        public void Establish()
        {
            var fakeStoreNotViewer = new FakeMessageStoreNotViewer();
            var modelFactory = new FakeMessageStoreViewerFactory(fakeStoreNotViewer, _storeName);
            _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
        }

        [Test]
        public void When_filtering_messages_for_existent_store_that_is_not_viewer()
        {
            _result = _messageListViewModelRetriever.Filter(_storeName, "term");

           // should_not_return_MessageListModel
            var model = _result.Result;
            Assert.Null(model);
            Assert.True(_result.IsError);
            Assert.AreEqual(MessageListModelError.StoreMessageViewerNotImplemented, _result.Error);
        }
   }

}