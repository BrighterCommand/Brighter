#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway
{
    public class ChannelRequeueTests
    {
        private readonly IAmAChannel _channel;
        private readonly IAmAMessageConsumerSupportingDelay _gateway;
        private readonly Message _requeueMessage;
        private readonly Stopwatch _stopWatch;

        public ChannelRequeueTests()
        {
            _gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => _gateway.DelaySupported).Returns(true);

            _channel = new Channel(new ChannelName("test"), _gateway);

            _requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            _stopWatch = new Stopwatch();
        }

        [Fact]
        public async Task When_Requeuing_A_Message_With_Supported_And_Enabled_Delay()
        {
            _stopWatch.Start();
            await _channel.RequeueAsync(_requeueMessage, 1000);
            _stopWatch.Stop();

            //_should_call_the_messaging_gateway
            A.CallTo(() => _gateway.RequeueAsync(_requeueMessage, 1000)).MustHaveHappened();
            //_should_have_used_gateway_delay_support
            _stopWatch.ElapsedMilliseconds.Should().BeLessThan(500);
        }
   }
}
