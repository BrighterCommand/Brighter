using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.MessageViewer.Ports.Domain;
using Paramore.Brighter.MessageViewer.Ports.ViewModelRetrievers;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.MessageListViewModelRetrieverTests
{
    public class MessageListViewModelRetrieverGetMessagesForStoreTests
    {
        private MessageListViewModelRetriever _messageListViewModelRetriever;
        private ViewModelRetrieverResult<MessageListModel, MessageListModelError> _result;
        private List<Message> _messages;
        private Message _message1;
        private string storeName = "testStore";

        public MessageListViewModelRetrieverGetMessagesForStoreTests()
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

        [Fact]
        public void When_retrieving_messages_for_a_store()
        {
            _result = _messageListViewModelRetriever.Get(storeName, 1);

            //should_return_MessageListModel
            var model = _result.Result;

            model.Should().NotBeNull();
            model.MessageCount.Should().Be(_messages.Count);

            //should_return_expected_message_state
            var foundMessage = model.Messages.Single(m => m.MessageId == _message1.Id);
            foundMessage.Should().NotBeNull();

            foundMessage.HandledCount.Should().Be(_message1.Header.HandledCount);
            foundMessage.MessageType.Should().Be(_message1.Header.MessageType.ToString());
            foundMessage.Topic.Should().Be(_message1.Header.Topic);
            foundMessage.TimeStamp.Should().Be(_message1.Header.TimeStamp);

            foreach (var key in _message1.Header.Bag.Keys)
            {
                foundMessage.Bag.Contains(key);
                foundMessage.Bag.Contains(_message1.Header.Bag[key].ToString());
            }
            foundMessage.MessageBody.Should().Be(_message1.Body.Value);

            //foundMessage.TimeStampUI.ShouldContain("ago");//fragile time-based
        }
   }
}