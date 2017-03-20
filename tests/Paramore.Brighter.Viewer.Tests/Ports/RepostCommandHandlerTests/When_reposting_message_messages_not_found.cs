using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Paramore.Brighter.MessageViewer.Ports.Handlers;
using Paramore.Brighter.Tests.TestDoubles;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.RepostCommandHandlerTests
{
    public class RepostCommandHandlerMessagesNotFoundTests
    {
        private readonly string _storeName = "storeItemtestStoreName";
        private RepostCommandHandler _repostHandler;
        private RepostCommand _command;
        private FakeMessageProducer _fakeMessageProducer;
        private Exception _ex;

        [SetUp]
        public void Establish()
        {
            var fakeStore = new FakeMessageStoreWithViewer();
            var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

            _command = new RepostCommand { MessageIds = new List<string> { Guid.NewGuid().ToString() }, StoreName = _storeName };
            _fakeMessageProducer = new FakeMessageProducer();
            _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)), new MessageRecoverer());
        }

        [Fact]
        public void When_reposting_message_messages_not_found()
        {
            _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            //should_throw_expected_exception
            Assert.IsInstanceOf<Exception>(_ex);
            StringAssert.Contains("messages", _ex.Message);
            StringAssert.Contains(_command.MessageIds.Single(), _ex.Message);
        }
  }

}