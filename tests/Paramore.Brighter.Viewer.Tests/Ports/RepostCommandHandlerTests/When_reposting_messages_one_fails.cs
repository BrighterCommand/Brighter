using System;
using System.Collections.Generic;
using Xunit;
using Paramore.Brighter.MessageViewer.Ports.Handlers;
using Paramore.Brighter.Tests.TestDoubles;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.RepostCommandHandlerTests
{
    public class RepostCommandHandlerMultipleMessagesOneFails
    {
        private string _storeName = "storeItemtestStoreName";
        private RepostCommandHandler _repostHandler;
        private RepostCommand _command;
        private Message _messageToRepost;
        private FakeMessageProducer _fakeMessageProducer;
        private Exception _ex;
        private Message _messageToRepostMissing;

        [SetUp]
        public void Establish()
        {
            var fakeStore = new FakeMessageStoreWithViewer();
            _messageToRepost = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
            fakeStore.Add(_messageToRepost);
            _messageToRepostMissing = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
            var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

            _command = new RepostCommand { MessageIds = new List<string> { _messageToRepost.Id.ToString(), _messageToRepostMissing.Id.ToString() }, StoreName = _storeName };
            _fakeMessageProducer = new FakeMessageProducer();
            _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)), new MessageRecoverer());
        }

        [Fact]
        public void When_reposting_messages_one_fails()
        {
            _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            //should_throw_expected_exception
            Assert.IsInstanceOf<Exception>(_ex);
            StringAssert.Contains("messages", _ex.Message);
            StringAssert.Contains(_messageToRepostMissing.Id.ToString(), _ex.Message);
        }
   }
}