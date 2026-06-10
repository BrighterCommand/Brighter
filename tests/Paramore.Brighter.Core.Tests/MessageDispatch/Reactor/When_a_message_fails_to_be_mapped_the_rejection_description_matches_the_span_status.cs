using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpMappingRejectionDescriptionMatchesSpanStatusTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly RoutingKey _invalidMessageKey = new("MyInvalidMessageTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;
        private readonly List<Activity> _exportedActivities;
        private readonly TracerProvider _traceProvider;
        private readonly string _messageId;

        public MessagePumpMappingRejectionDescriptionMatchesSpanStatusTests()
        {
            _exportedActivities = new List<Activity>();
            _traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .AddInMemoryExporter(_exportedActivities)
                .Build();

            var tracer = new BrighterTracer(_timeProvider);
            var instrumentationOptions = InstrumentationOptions.All;

            _channel = new Channel(
                new(ChannelName), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider,
                    invalidMessageTopic: _invalidMessageKey,
                    ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new FailingEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyFailingMapperEvent, FailingEventMessageMapper>();
            var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => throw new NotImplementedException());

            _messagePump = new ServiceActivator.Reactor(
                new SpyRequeueCommandProcessor(),
                (message) => typeof(MyFailingMapperEvent),
                messageMapperRegistry,
                messageTransformerFactory,
                new InMemoryRequestContextFactory(),
                _channel,
                tracer,
                instrumentationOptions)
            {
                Channel = _channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = 3,
                UnacceptableMessageLimit = 3
            };

            _messageId = Guid.NewGuid().ToString();
            var unmappableMessage = new Message(
                new MessageHeader(_messageId, _routingKey, MessageType.MT_EVENT),
                new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            _channel.Enqueue(unmappableMessage);
            _channel.Stop(_routingKey);
        }

        [Fact]
        public void When_A_Message_Fails_To_Be_Mapped_The_Rejection_Description_Matches_The_Span_Status()
        {
            // Act
            _messagePump.Run();
            _traceProvider.ForceFlush();

            // Assert — mechanism (A): rejected message carries the rejection reason in its header bag
            var rejectedMessage = _bus.Stream(_invalidMessageKey).First();
            Assert.True(rejectedMessage.Header.Bag.TryGetValue(Message.RejectionReasonHeaderName, out var bagValue));
            var bagString = Assert.IsType<string>(bagValue);
            Assert.NotEmpty(bagString);
            Assert.Contains(_messageId, bagString);

            // Assert — mechanism (B): process span StatusDescription contains the message Id
            var processActivity = _exportedActivities.FirstOrDefault(a =>
                a.DisplayName == $"{_routingKey} {MessagePumpSpanOperation.Process.ToSpanName()}");
            Assert.NotNull(processActivity);
            Assert.Equal(ActivityStatusCode.Error, processActivity!.Status);
            Assert.NotNull(processActivity.StatusDescription);
            Assert.Contains(_messageId, processActivity.StatusDescription);

            // The bag value embeds the span StatusDescription (both derive from the same shared description local — C-5)
            Assert.Contains(processActivity.StatusDescription!, bagString);
        }
    }
}
