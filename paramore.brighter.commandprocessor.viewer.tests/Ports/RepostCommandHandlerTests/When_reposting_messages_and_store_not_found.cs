using System;
using System.Collections.Generic;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports.RepostCommandHandlerTests
{
    public class RepostCommandHandlerMissingStoreTests
    {
        private readonly string _storeName = "storeItemtestStoreName";
        private RepostCommandHandler _repostHandler;
        private RepostCommand _command;
        private FakeMessageProducer _fakeMessageProducer;
        private Exception _ex;

        [SetUp]
        public void Establish()
        {
            var fakeMessageStoreFactory = FakeMessageStoreViewerFactory.CreateEmptyFactory();

            _command = new RepostCommand { MessageIds = new List<string> { Guid.NewGuid().ToString() }, StoreName = _storeName };
            _fakeMessageProducer = new FakeMessageProducer();
            _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)), new MessageRecoverer());
        }

        [Test]
        public void When_reposting_messages_and_store_not_found()
        {
            _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            //should_throw_expected_exception
            _ex.ShouldBeOfExactType<Exception>();
            _ex.Message.ShouldContain("Store");
        }
   }

}