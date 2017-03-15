using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.MessageViewer.Ports.Domain;
using Paramore.Brighter.MessageViewer.Ports.ViewModelRetrievers;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.MessageListViewModelRetrieverTests
{
    [TestFixture]
    public class MessageListModelRetrieverFilterOnTopicTests
    {
        private MessageListViewModelRetriever _messageListViewModelRetriever;
        private ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        private List<Message> _messages;
        private readonly string _storeName = "testStore";

        [SetUp]
        public void Establish()
        {
            _messages = new List<Message>{
                new Message(new MessageHeader(Guid.NewGuid(), "MyTopic1", MessageType.MT_COMMAND), new MessageBody("")),
                new Message(new MessageHeader(Guid.NewGuid(), "MyTopic2", MessageType.MT_COMMAND), new MessageBody(""))};

            var fakeStore = new FakeMessageStoreWithViewer();
            _messages.ForEach(m => fakeStore.Add(m));
            var modelFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);
            _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
        }

        [Test]
        public void When_searching_messages_for_matching_rows_topic()
        {
            _result = _messageListViewModelRetriever.Filter(_storeName, "MyTopic");

           //should_return_expected_model
           var model = _result.Result;

            Assert.NotNull(model);
            Assert.AreEqual(_messages.Count, model.MessageCount);
        }
   }
}