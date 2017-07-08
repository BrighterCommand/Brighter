using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway
{
    public class ChannelRequeueWithDelayTests
    {
        private readonly IAmAChannel _channel;
        private readonly IAmAMessageConsumerSupportingDelay _gateway;
        private readonly Message _requeueMessage;
        private readonly Stopwatch _stopWatch;

        public ChannelRequeueWithDelayTests()
        {
            _gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => _gateway.DelaySupported).Returns(false);

            _channel = new Channel(new ChannelName("test"), _gateway);

            _requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            _stopWatch = new Stopwatch();
        }

        [Fact]
        public async Task When_Requeuing_A_Message_With_Supported_But_Disabled_Delay()
        {
            _stopWatch.Start();
            await _channel.RequeueAsync(_requeueMessage, 1000);
            _stopWatch.Stop();

            //_should_call_the_messaging_gateway
            A.CallTo(() => _gateway.RequeueAsync(_requeueMessage)).MustHaveHappened();
            //_should_have_process_delayed_the_call
            _stopWatch.ElapsedMilliseconds.Should().BeGreaterThan(900);
        }
    }
}