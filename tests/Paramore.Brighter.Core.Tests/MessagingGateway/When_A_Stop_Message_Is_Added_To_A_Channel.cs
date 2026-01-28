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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway
{
    public class ChannelStopTests
    {
        private readonly RoutingKey _routingKey = new("myTopic");
        private const string ChannelName = "myChannel";
        private readonly IAmAChannelSync _channel;
        private readonly InternalBus _bus;

        public ChannelStopTests()
        {
            _bus = new InternalBus();
            IAmAMessageConsumerSync gateway = new InMemoryMessageConsumer(_routingKey, _bus, TimeProvider.System, ackTimeout: TimeSpan.FromMilliseconds(1000)); 

            _channel = new Channel(new(ChannelName),_routingKey, gateway);

            Message sentMessage = new(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
                new MessageBody("a test body"));
            
            _bus.Enqueue(sentMessage);

            _channel.Stop(_routingKey);
        }

        [Fact]
        public void When_A_Stop_Message_Is_Added_To_A_Channel()
        {
            var stopMessage = _channel.Receive(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(MessageType.MT_QUIT, stopMessage.Header.MessageType);
            
            Assert.Single(_bus.Stream(new RoutingKey(_routingKey)));
        }
    }
}
