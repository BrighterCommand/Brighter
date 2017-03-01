using System;
using System.Diagnostics;
using FakeItEasy;
using NUnit.Framework;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway
{
    [TestFixture]
    public class ChannelRequeueWithDelayTests
    {
        private IAmAChannel _channel;
        private IAmAMessageConsumerSupportingDelay _gateway;
        private Message _requeueMessage;
        private Stopwatch _stopWatch;

        [SetUp]
        public void Establish()
        {
            _gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => _gateway.DelaySupported).Returns(false);

            _channel = new Channel("test", _gateway);

            _requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            _stopWatch = new Stopwatch();
        }

        [Test]
        public void When_Requeuing_A_Message_With_Supported_But_Disabled_Delay()
        {
            _stopWatch.Start();
            _channel.Requeue(_requeueMessage, 1000);
            _stopWatch.Stop();

            //_should_call_the_messaging_gateway
            A.CallTo(() => _gateway.Requeue(_requeueMessage)).MustHaveHappened();
            //_should_have_process_delayed_the_call
            Assert.True((_stopWatch.ElapsedMilliseconds > 900));
        }
    }
}