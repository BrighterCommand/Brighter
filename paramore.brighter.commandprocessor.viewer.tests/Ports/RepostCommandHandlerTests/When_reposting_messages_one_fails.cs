using System;
using System.Collections.Generic;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports.RepostCommandHandlerTests
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

        [Test]
        public void When_reposting_messages_one_fails()
        {
            _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            //should_throw_expected_exception
            _ex.ShouldBeOfExactType<Exception>();
            _ex.Message.ShouldContain("messages");
            _ex.Message.ShouldContain(_messageToRepostMissing.Id.ToString());

        }
   }
}