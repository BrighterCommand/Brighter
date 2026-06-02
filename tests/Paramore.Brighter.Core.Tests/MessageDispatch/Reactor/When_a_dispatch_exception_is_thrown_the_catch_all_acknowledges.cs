using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpDispatchExceptionCatchAllAcknowledgesTests
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

        public MessagePumpDispatchExceptionCatchAllAcknowledgesTests()
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
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var requestContextFactory = new InMemoryRequestContextFactory();

            // SpyExceptionCommandProcessor.Publish throws AggregateException — drives the dispatch catch-all path
            _messagePump = new ServiceActivator.Reactor(
                new SpyExceptionCommandProcessor(),
                (message) => typeof(MyEvent),
                messageMapperRegistry,
                null,
                requestContextFactory,
                _channel,
                tracer,
                instrumentationOptions)
            {
                Channel = _channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = 3
            };

            // Build a properly-mapped event message so that mapping succeeds and the exception comes from dispatch
            var mappableMessage = new TransformPipelineBuilder(messageMapperRegistry, null)
                .BuildWrapPipeline<MyEvent>()
                .Wrap(new MyEvent(), requestContextFactory.Create(), new Publication { Topic = _routingKey });

            _channel.Enqueue(mappableMessage);
            _channel.Stop(_routingKey);
        }

        [Fact]
        public void When_A_Dispatch_Exception_Is_Thrown_The_Catch_All_Acknowledges()
        {
            // Act
            _messagePump.Run();
            _traceProvider.ForceFlush();

            // Assert — mechanism (A): zero messages on the invalid-message topic; dispatch exceptions do NOT reject
            Assert.Empty(_bus.Stream(_invalidMessageKey));

            // Assert — mechanism (B): the process span was exported with Error status and ended (span exported ⇒ EndSpan ran)
            var processActivity = _exportedActivities.FirstOrDefault(a =>
                a.DisplayName == $"{_routingKey} {MessagePumpSpanOperation.Process.ToSpanName()}");
            Assert.NotNull(processActivity);
            Assert.Equal(ActivityStatusCode.Error, processActivity!.Status);
        }
    }
}
