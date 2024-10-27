﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
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
                tracer: tracer,
                instrumentationOptions: instrumentationOptions);

            var provider = new CommandProcessorProvider(commandProcessor);
            
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
            
            _messagePump = new MessagePumpBlocking<MyEvent>(provider, messageMapperRegistry, null, 
                new InMemoryRequestContextFactory(), channel, tracer, instrumentationOptions)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), EmptyChannelDelay = 1000
            };
            
            var externalActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("MessagePumpSpanTests");
            
            var header = new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT)
            {
                TraceParent = externalActivity?.Id, TraceState = externalActivity?.TraceStateString
            };
            
            externalActivity?.Stop();

            _message = new Message(
                header, 
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(_message);
            
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
    }

    [Fact]
    public void When_There_Is_A_BrokenCircuit_Channel_Failure_Close_The_Span()
    {
        _messagePump.Run();

        _traceProvider.ForceFlush();
            
        _exportedActivities.Count.Should().Be(7);
        _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue(); 
        
        var errorMessageActivity = _exportedActivities.FirstOrDefault(a => 
            a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Receive.ToSpanName()}"
            && a.Status == ActivityStatusCode.Error
        );
        
        errorMessageActivity.Should().NotBeNull();
        errorMessageActivity?.Status.Should().Be(ActivityStatusCode.Error);
    }
}
