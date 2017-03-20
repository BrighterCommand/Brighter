using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessageViewer.Ports.Handlers;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.RepostCommandHandlerTests
{
   public class RepostCommandHandlerMisConfiguredTests
    {
        private string _storeName = "storeItemtestStoreName";
        private RepostCommandHandler _repostHandler;
        private RepostCommand _command;
        private Message _messageToRepost;

        public RepostCommandHandlerMisConfiguredTests()
        {
            var fakeStore = new FakeMessageStoreWithViewer();
            _messageToRepost = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
            fakeStore.Add(_messageToRepost);
            var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

            _command = new RepostCommand { MessageIds = new List<string> { _messageToRepost.Header.Id.ToString() }, StoreName = _storeName };
            _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeErrorProducingMessageProducerFactory()), new MessageRecoverer());
        }

        [Fact]
        public void When_reposting_message_broker_cannot_be_created()
        {
            var ex = Catch.Exception(() => _repostHandler.Handle(_command));

            //should_throw_expected_exception
            ex.Should().BeOfType<Exception>();
            ex.Message.Should().Contain("Mis-configured");
        }
   }
}

