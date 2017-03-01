using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports.MessageListViewModelRetrieverTests
{
    public class MessageListViewModelRetrieverGetMessagesForStoreTests
    {
        private MessageListViewModelRetriever _messageListViewModelRetriever;
        private ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        private List<Message> _messages;
        private Message _message1;
        private string storeName = "testStore";

        [SetUp]
        public void Establish()
        {
            _message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic1", MessageType.MT_COMMAND), new MessageBody("a body"));
            var messageHeader = new MessageHeader(Guid.NewGuid(), "MyTopic2", MessageType.MT_COMMAND);
            messageHeader.Bag.Add("bagVal1", "value1");
            messageHeader.Bag.Add("bagVal2", "value2");
            _messages = new List<Message>{
                _message1,
                new Message(messageHeader, new MessageBody(""))};

            var fakeStore = new FakeMessageStoreWithViewer();
            _messages.ForEach(m => fakeStore.Add(m));
            var modelFactory = new FakeMessageStoreViewerFactory(fakeStore, storeName);
            _messageListViewModelRetriever = new MessageListViewModelRetriever(modelFactory);
        }

        [Test]
        public void When_retrieving_messages_for_a_store()
        {
            _result = _messageListViewModelRetriever.Get(storeName, 1);

            //should_return_MessageListModel
            var model = _result.Result;

            Assert.NotNull(model);
            Assert.AreEqual(_messages.Count, model.MessageCount);

            //should_return_expected_message_state
            var foundMessage = model.Messages.Single(m => m.MessageId == _message1.Id);
            Assert.NotNull(foundMessage);

            Assert.AreEqual(_message1.Header.HandledCount, foundMessage.HandledCount);
            Assert.AreEqual(_message1.Header.MessageType.ToString(), foundMessage.MessageType);
            Assert.AreEqual(_message1.Header.Topic, foundMessage.Topic);
            Assert.AreEqual(_message1.Header.TimeStamp, foundMessage.TimeStamp);

            foreach (var key in _message1.Header.Bag.Keys)
            {
                foundMessage.Bag.Contains(key);
                foundMessage.Bag.Contains(_message1.Header.Bag[key].ToString());
            }
            Assert.AreEqual(_message1.Body.Value, foundMessage.MessageBody);

            //foundMessage.TimeStampUI.ShouldContain("ago");//fragile time-based
        }

   }

}