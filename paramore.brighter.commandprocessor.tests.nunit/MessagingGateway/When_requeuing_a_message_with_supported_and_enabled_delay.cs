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
using FakeItEasy;
using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway
{
    [TestFixture]
    public class ChannelRequeueTests
    {
        private IAmAChannel _channel;
        private IAmAMessageConsumerSupportingDelay _gateway;
        private Message _requeueMessage;
        private Stopwatch _stopWatch;

        [SetUp]
        public void Establish()
        {
            _gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => _gateway.DelaySupported).Returns(true);

            _channel = new Channel("test", _gateway);

            _requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            _stopWatch = new Stopwatch();
        }

        [Test]
        public void When_Requeuing_A_Message_With_Supported_And_Enabled_Delay()
        {
            _stopWatch.Start();
            _channel.Requeue(_requeueMessage, 1000);
            _stopWatch.Stop();

            //_should_call_the_messaging_gateway
            A.CallTo(() => _gateway.Requeue(_requeueMessage, 1000)).MustHaveHappened();
            //_should_have_used_gateway_delay_support
            (_stopWatch.ElapsedMilliseconds < 500).ShouldBeTrue();
        }
   }
}
