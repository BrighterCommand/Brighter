using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;
[NotInParallel]
public class MessagePumpEmptyQueueOberservabilityTests
{
    private const string Topic = "MyTopic";
    private const string ChannelName = "myChannel";
    private readonly RoutingKey _routingKey = new(Topic);
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IAmAMessagePump _messagePump;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Message _message;
    public MessagePumpEmptyQueueOberservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));
        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        var instrumentationOptions = InstrumentationOptions.All;
        var commandProcessor = new Brighter.CommandProcessor(subscriberRegistry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: instrumentationOptions);
        Channel channel = new(new(ChannelName), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));
        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()), null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        _messagePump = new Reactor(commandProcessor, (message) => typeof(MyEvent), messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel, tracer, instrumentationOptions)
        {
            Channel = channel,
            TimeOut = TimeSpan.FromMilliseconds(5000),
            EmptyChannelDelay = TimeSpan.FromMilliseconds(1000)
        };
        //in theory the message pump should see this from the consumer when the queue is empty
        //we are just forcing this to run exactly once, before we quit.
        _message = MessageFactory.CreateEmptyMessage(_routingKey);
        channel.Enqueue(_message);
        var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
        channel.Enqueue(quitMessage);
    }

    [Test]
    public async Task When_There_Are_No_Messages_Close_The_Span()
    {
        _messagePump.Run();
        _traceProvider.ForceFlush();
        await Assert.That(_exportedActivities.Count).IsEqualTo(3);
        await Assert.That((_exportedActivities).Any(a => a.Source.Name == "Paramore.Brighter")).IsTrue();
        var emptyMessageActivity = _exportedActivities.FirstOrDefault(a => a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Receive.ToSpanName()}" && a.TagObjects.Any(t => t is { Value: not null, Key: BrighterSemanticConventions.MessageType } && Enum.Parse<MessageType>(t.Value.ToString()) == MessageType.MT_NONE));
        await Assert.That(emptyMessageActivity).IsNotNull();
        await Assert.That(emptyMessageActivity!.Status).IsEqualTo(ActivityStatusCode.Ok);
    }
}
