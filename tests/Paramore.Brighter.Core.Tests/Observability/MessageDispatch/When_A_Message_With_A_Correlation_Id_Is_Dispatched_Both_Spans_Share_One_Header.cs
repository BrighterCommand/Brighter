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
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch
{
    public class MessageHeaderCorrelationIdObservabilityTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new();
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly List<Activity> _exportedActivities;
        private readonly TracerProvider _traceProvider;
        private readonly Message _message;
        private const string CorrelationId = "my-correlation-id";

        public MessageHeaderCorrelationIdObservabilityTests()
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            _exportedActivities = new List<Activity>();

            _traceProvider = builder
                .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();

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

            _message = new Message(
                new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT, correlationId: new Id(CorrelationId),
                    replyTo: new RoutingKey("io.paramorebrighter.myevent")),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            channel.Enqueue(_message);
            var quitMessage = MessageFactory.CreateQuitMessage(new RoutingKey("MyTopic"));
            channel.Enqueue(quitMessage);
        }

        [Test]
        public async Task When_a_message_with_a_correlation_id_is_dispatched_both_spans_share_one_header()
        {
            // Act
            _messagePump.Run();
            _traceProvider.ForceFlush();

            // Assert
            var receiveSpan = _exportedActivities.FirstOrDefault(a =>
                a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Receive.ToSpanName()}"
                && a.TagObjects.Any(to => to is { Value: not null, Key: BrighterSemanticConventions.MessageType } && Enum.Parse<MessageType>(to.Value.ToString() ?? string.Empty) == MessageType.MT_EVENT));
            await Assert.That(receiveSpan).IsNotNull();

            var processSpan = _exportedActivities.FirstOrDefault(a =>
                a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Process.ToSpanName()}"
                && a.TagObjects.Any(to => to is { Value: not null, Key: BrighterSemanticConventions.MessageType } && Enum.Parse<MessageType>(to.Value.ToString() ?? string.Empty) == MessageType.MT_EVENT));
            await Assert.That(processSpan).IsNotNull();

            var receiveHeaderJson = (string?)receiveSpan!.TagObjects.Single(to => to.Key == BrighterSemanticConventions.MessageHeaders).Value;
            var processHeaderJson = (string?)processSpan!.TagObjects.Single(to => to.Key == BrighterSemanticConventions.MessageHeaders).Value;

            //the correlation id is carried in the serialized header as the top-level CorrelationId field
            await Assert.That(receiveHeaderJson).Contains(CorrelationId);
            await Assert.That(processHeaderJson).Contains(CorrelationId);

            //both spans share one serialized header (Assert.Same: a never-interned string, so equal references prove reuse)
            await Assert.That(processHeaderJson).IsEqualTo(receiveHeaderJson);
            await Assert.That(processHeaderJson).IsSameReferenceAs(receiveHeaderJson);

            await Assert.That(receiveSpan.TagObjects.Single(to => to.Key == BrighterSemanticConventions.ConversationId).Value).IsEqualTo(CorrelationId);
            await Assert.That(processSpan.TagObjects.Single(to => to.Key == BrighterSemanticConventions.ConversationId).Value).IsEqualTo(CorrelationId);
        }
    }
}