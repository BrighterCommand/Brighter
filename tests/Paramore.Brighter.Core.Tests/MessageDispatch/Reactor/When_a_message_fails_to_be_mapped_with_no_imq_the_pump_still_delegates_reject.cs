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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpFailingMessageTranslationNoImqTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly RoutingKey _deadLetterKey = new("MyDeadLetterTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;

        public MessagePumpFailingMessageTranslationNoImqTests()
        {
            // No invalidMessageTopic — only a deadLetterTopic
            _channel = new Channel(
                new(ChannelName), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider,
                    deadLetterTopic: _deadLetterKey,
                    ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new FailingEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyFailingMapperEvent, FailingEventMessageMapper>();

            _messagePump = new ServiceActivator.Reactor(
                new SpyRequeueCommandProcessor(),
                (message) => typeof(MyFailingMapperEvent),
                messageMapperRegistry,
                null,
                new InMemoryRequestContextFactory(),
                _channel)
            {
                Channel = _channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = 3
            };

            var unmappableMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
                new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            _channel.Enqueue(unmappableMessage);
            _channel.Stop(_routingKey);
        }

        [Fact]
        public void When_A_Message_Fails_To_Be_Mapped_With_No_Imq_The_Pump_Still_Delegates_Reject()
        {
            // Act
            _messagePump.Run();

            // Assert — Reject(Unacceptable) falls back to dead-letter topic when no IMQ configured
            Assert.Single(_bus.Stream(_deadLetterKey));
            Assert.Empty(_bus.Stream(_routingKey));
        }
    }
}
