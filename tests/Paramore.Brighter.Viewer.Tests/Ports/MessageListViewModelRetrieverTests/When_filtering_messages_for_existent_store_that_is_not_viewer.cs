using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.MessageViewer.Ports.Domain;
using Paramore.Brighter.MessageViewer.Ports.ViewModelRetrievers;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.MessageListViewModelRetrieverTests
{
    public class MessageListViewModelRetreiverStoreNotInViewerTests
    {
        private MessageListViewModelRetriever _messageListViewModelRetriever;
        private ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        private readonly string _storeName = "storeNotImplementingViewer";

        public MessageListViewModelRetreiverStoreNotInViewerTests()
        {
            var fakeStoreNotViewer = new FakeMessageStoreNotViewer();
            var modelFactory = new FakeMessageStoreViewerFactory(fakeStoreNotViewer, _storeName);
            _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
        }

        [Fact]
        public void When_filtering_messages_for_existent_store_that_is_not_viewer()
        {
            _result = _messageListViewModelRetriever.Filter(_storeName, "term");

           // should_not_return_MessageListModel
            var model = _result.Result;
            model.Should().BeNull();
            _result.IsError.Should().BeTrue();
            _result.Error.Should().Be(MessageListModelError.StoreMessageViewerNotImplemented);
        }
   }
}