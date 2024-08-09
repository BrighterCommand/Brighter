using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

public class MessagePumpEmptyQueueOberservabilityTests
{
    private const string Topic = "MyTopic";
    private readonly RoutingKey _routingKey = new(Topic);
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IAmAMessagePump _messagePump;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Message _message;
    private readonly Channel _channel;

    public MessagePumpEmptyQueueOberservabilityTests()
    {
            var builder = Sdk.CreateTracerProviderBuilder();
            _exportedActivities = new List<Activity>();

            _traceProvider = builder
                .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();
        
            Brighter.CommandProcessor.ClearServiceBus();
            
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));
            
            var timeProvider  = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var instrumentationOptions = InstrumentationOptions.All;
            
            var commandProcessor = new Brighter.CommandProcessor(
                subscriberRegistry,
                handlerFactory, 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(),
                tracer: tracer,
                instrumentationOptions: instrumentationOptions);

            var provider = new CommandProcessorProvider(commandProcessor);
            
            PipelineBuilder<MyEvent>.ClearPipelineCache();

            _channel = new Channel(Topic, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, 1000));
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(
                    _ => new MyEventMessageMapper()),
                null); 
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new MessagePumpBlocking<MyEvent>(provider, messageMapperRegistry, null, 
                new InMemoryRequestContextFactory(), tracer, instrumentationOptions)
            {
                Channel = _channel, TimeoutInMilliseconds = 5000, EmptyChannelDelay = 1000
            };
            
            new ActivitySource("Paramore.Brighter.Tests").StartActivity("MessagePumpSpanTests");

            _message = MessageFactory.CreateEmptyMessage(_routingKey);
            
            _channel.Enqueue(_message);
            
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            _channel.Enqueue(quitMessage);
    }

    [Fact]
    public void When_There_Are_No_Messages_Close_The_Span()
    {
        _messagePump.Run();

        _traceProvider.ForceFlush();
            
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue(); 
        
        var emptyMessageActivity = _exportedActivities.FirstOrDefault(a => 
            a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Receive.ToSpanName()}" 
            && a.TagObjects.Any(t => 
                t is { Value: not null, Key: BrighterSemanticConventions.MessageType } 
                && Enum.Parse<MessageType>(t.Value.ToString()) == MessageType.MT_NONE
                )
            );
        
        emptyMessageActivity.Should().NotBeNull();
        emptyMessageActivity.ParentId.Should().Be(_message.Header.TraceParent);
        emptyMessageActivity.Status.Should().Be(ActivityStatusCode.Ok);
        emptyMessageActivity.StatusDescription.Should().Be($"MessagePump: No queued messages on {_channel.Name} sleeping for 1000 ms");
        
    }
}
