using System;
using System.Threading.Tasks;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    [TestFixture]
    public class MessagePumpFailingMessageTranslationTests
    {
        private IAmAMessagePump _messagePump;
        private FakeChannel _channel;
        private SpyRequeueCommandProcessor _commandProcessor;

        [SetUp]
        public void Establish()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            _channel = new FakeChannel();
            var mapper = new FailingEventMessageMapper();
            _messagePump = new MessagePump<MyFailingMapperEvent>(_commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3 };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            _channel.Add(unmappableMessage);
        }

        [Test]
        public void When_A_Message_Fails_To_Be_Mapped_To_A_Request ()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            _channel.Stop();

            Task.WaitAll(new[] { task });

            //should_have_acknowledge_the_message
            _channel.AcknowledgeHappened.ShouldBeTrue();
        }
    }
}