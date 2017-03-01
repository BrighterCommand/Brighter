using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports.MessageListViewModelRetrieverTests
{
   public class MessageListVIewModelRetrieverFilterNonExistantStoreTests
    {
        private MessageListViewModelRetriever _messageListViewModelRetriever;
        private ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        private static string storeName = "storeNamenotInStore";

        [SetUp]
        public void Establish()
        {
            var modelFactory = FakeMessageStoreViewerFactory.CreateEmptyFactory();
            _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
        }

        [Test]
        public void When_filtering_messages_for_non_existent_store()
        {
            _result = _messageListViewModelRetriever.Filter(storeName, "term");

            //should_not_return_MessageListModel
             var model = _result.Result;
            model.ShouldBeNull();
            _result.IsError.ShouldBeTrue();
            _result.Error.ShouldEqual(MessageListModelError.StoreNotFound);
        }
   }
}