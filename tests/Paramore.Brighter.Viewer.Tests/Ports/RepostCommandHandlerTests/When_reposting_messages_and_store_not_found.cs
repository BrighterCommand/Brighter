using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessageViewer.Ports.Handlers;
using Paramore.Brighter.Tests.TestDoubles;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Ports.RepostCommandHandlerTests
{
    public class RepostCommandHandlerMissingStoreTests
    {
        private readonly string _storeName = "storeItemtestStoreName";
        private RepostCommandHandler _repostHandler;
        private RepostCommand _command;
        private FakeMessageProducer _fakeMessageProducer;
        private Exception _ex;

        public RepostCommandHandlerMissingStoreTests()
        {
            var fakeMessageStoreFactory = FakeMessageStoreViewerFactory.CreateEmptyFactory();

            _command = new RepostCommand { MessageIds = new List<string> { Guid.NewGuid().ToString() }, StoreName = _storeName };
            _fakeMessageProducer = new FakeMessageProducer();
            _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)), new MessageRecoverer());
        }

        [Fact]
        public void When_reposting_messages_and_store_not_found()
        {
            _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            //should_throw_expected_exception
            _ex.Should().BeOfType<Exception>();
            _ex.Message.Should().Contain("Store");
        }
   }

}