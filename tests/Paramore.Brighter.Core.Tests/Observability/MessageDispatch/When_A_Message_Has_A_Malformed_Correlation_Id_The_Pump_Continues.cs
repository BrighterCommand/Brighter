#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch
{
    public class MalformedCorrelationIdPumpObservabilityTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new();
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();

        public MalformedCorrelationIdPumpObservabilityTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));

            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var instrumentationOptions = InstrumentationOptions.All;

            var commandProcessor = new Brighter.CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory(),
                tracer: tracer,
                instrumentationOptions: instrumentationOptions);

            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new Channel(
                new(ChannelName), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(
                    _ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            _messagePump = new Reactor(commandProcessor, (message) => typeof(MyEvent),
                messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel, tracer, instrumentationOptions)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
            };

            //a serviceable message whose correlation id is rejected by W3C Baggage validation: propagating it as baggage
            //throws, but observability must never tear down the pump - the message should still be dispatched
            var message = new Message(
                new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT, correlationId: new Id("bad=value")),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            channel.Enqueue(message);
            var quitMessage = MessageFactory.CreateQuitMessage(new RoutingKey("MyTopic"));
            channel.Enqueue(quitMessage);
        }

        [Fact]
        public void When_a_message_has_a_malformed_correlation_id_the_pump_continues()
        {
            //Act - a malformed correlation id makes baggage propagation throw; the pump must not propagate that out
            var exception = Record.Exception(() => _messagePump.Run());

            //Assert - the pump ran to its QUIT message without tearing down, and the poisoned message was still dispatched
            Assert.Null(exception);
            Assert.True(_receivedMessages.ContainsKey(nameof(MyEventHandler)));
            Assert.Equal(MessagePumpStatus.MP_STOPPED, _messagePump.Status);
        }
    }
}
