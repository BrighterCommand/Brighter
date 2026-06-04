#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpUnacceptableMessageLimitTestsAsync
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly RoutingKey _invalidMessageKey = new("MyInvalidMessageTopic");
        private readonly InternalBus _bus = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly FakeTimeProvider _timeProvider = new();

        public MessagePumpUnacceptableMessageLimitTestsAsync()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            var channel = new ChannelAsync(
                new (Channel), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider,
                    invalidMessageTopic: _invalidMessageKey,
                    ackTimeout: TimeSpan.FromMilliseconds(1000)),
                2
            );
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new FailingEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyFailingMapperEvent, FailingEventMessageMapperAsync>();

            _messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyFailingMapperEvent),
                messageMapperRegistry, null, new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3, UnacceptableMessageLimit = 3
            };

            var unmappableMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
                new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }")
            );

            channel.Enqueue(unmappableMessage);
            channel.Enqueue(unmappableMessage);
            channel.Enqueue(unmappableMessage);

        }

        [Fact]
        public void When_A_Message_Fails_To_Be_Mapped_To_A_Request_And_The_Unacceptable_Message_Limit_Is_Reached()
        {
            // Act — pump terminates on its own when UnacceptableMessageLimitReached fires on the 4th iteration
            _messagePump.Run();

            // Assert — each of the 3 mapping failures was rejected (not acknowledged)
            Assert.Equal(3, _bus.Stream(_invalidMessageKey).Count());

            // Assert — source queue is empty (no fall-through acknowledge)
            Assert.Empty(_bus.Stream(_routingKey));

            // Assert — pump terminated because the unacceptable message limit was reached
            Assert.Equal(MessagePumpStatus.MP_LIMIT_EXCEEDED, _messagePump.Status);
        }
    }
}
