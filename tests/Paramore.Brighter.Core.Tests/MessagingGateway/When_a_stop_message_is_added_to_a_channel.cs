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
using FakeItEasy;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway
{
    public class ChannelStopTests
    {
        private readonly IAmAChannel _channel;
        private readonly IAmAMessageConsumer _gateway;
        private Message _receivedMessage;
        private readonly Message _sentMessage;

        public ChannelStopTests()
        {
            _gateway = A.Fake<IAmAMessageConsumer>();

            _channel = new Channel("test", _gateway);

            _sentMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            _channel.Stop();

            A.CallTo(() => _gateway.Receive(1000)).Returns(new Message[] {_sentMessage});
        }

        [Fact]
        public void When_A_Stop_Message_Is_Added_To_A_Channel()
        {
            _receivedMessage = _channel.Receive(1000);

            //_should_call_the_messaging_gateway
            A.CallTo(() => _gateway.Receive(1000)).MustNotHaveHappened();
        }
    }
}
