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
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

public class MessagePumpBrokenCircuitChannelFailureOberservabilityTests
{
    private const string ChannelName = "myChannel";
    private readonly RoutingKey _routingKey = new("MyTopic");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IAmAMessagePump _messagePump;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly MyEvent _myEvent = new();
    private readonly Message _message;

    public MessagePumpBrokenCircuitChannelFailureOberservabilityTests()
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
                new InMemorySchedulerFactory(),
                tracer: tracer,
                instrumentationOptions: instrumentationOptions);
            
            PipelineBuilder<MyEvent>.ClearPipelineCache();

            FailingChannel channel = new(
                new (ChannelName), 
                _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)),
                brokenCircuit: true);
            
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(
                    _ => new MyEventMessageMapper()),
                null); 
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new Reactor(commandProcessor, (message) => typeof(MyEvent), 
                messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel, tracer, instrumentationOptions)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), EmptyChannelDelay = TimeSpan.FromMilliseconds(1000)
            };
            
            var externalActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("MessagePumpSpanTests");
            
            _message = new Message(
                new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );
            
            var contextPropogator = new TextContextPropogator();
            contextPropogator.PropogateContext(externalActivity?.Context, _message);
            
            externalActivity?.Stop();

            channel.Enqueue(_message);
            
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
            
    }

    [Fact]
    public void When_There_Is_A_BrokenCircuit_Channel_Failure_Close_The_Span()
    {
        _messagePump.Run();

        _traceProvider.ForceFlush();
            
        Assert.Equal(7, _exportedActivities.Count);
        Assert.True(_exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter")); 
        
        var errorMessageActivity = _exportedActivities.FirstOrDefault(a => 
            a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Receive.ToSpanName()}"
            && a.Status == ActivityStatusCode.Error
        );
        
        Assert.NotNull(errorMessageActivity);
        Assert.Equal(ActivityStatusCode.Error, errorMessageActivity?.Status);
    }
}
