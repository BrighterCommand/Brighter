#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

/// <summary>
/// Verifies the fix for issue #4085: receive span and process span are created as siblings — receive
/// covers the broker call only and process covers dispatch. Process span carries the producer
/// traceparent so handlers descend from the producer trace.
/// </summary>
public class MessagePumpProcessSpanObservabilityTests
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

    public MessagePumpProcessSpanObservabilityTests()
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

        var tracer = new BrighterTracer(new FakeTimeProvider());
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
            new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _messagePump = new Reactor(commandProcessor, _ => typeof(MyEvent),
            messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel, tracer, instrumentationOptions)
        {
            Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
        };

        var producerActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("Producer");

        _message = new Message(
            new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT),
            new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
        );

        new TextContextPropogator().PropogateContext(producerActivity?.Context, _message);
        producerActivity?.Stop();

        channel.Enqueue(_message);
        channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
    }

    [Fact]
    public void When_A_Message_Is_Processed_A_Process_Span_Is_Created()
    {
        _messagePump.Run();

        _traceProvider.ForceFlush();

        var receiveSpan = _exportedActivities.SingleOrDefault(a =>
            a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Receive.ToSpanName()}"
            && a.TagObjects.Any(t => t is { Key: BrighterSemanticConventions.MessageType }
                                     && Enum.Parse<MessageType>(t.Value!.ToString()!) == MessageType.MT_EVENT));

        var processSpan = _exportedActivities.SingleOrDefault(a =>
            a.DisplayName == $"{_message.Header.Topic} {MessagePumpSpanOperation.Process.ToSpanName()}"
            && a.TagObjects.Any(t => t is { Key: BrighterSemanticConventions.MessageType }
                                     && Enum.Parse<MessageType>(t.Value!.ToString()!) == MessageType.MT_EVENT));

        Assert.NotNull(receiveSpan);
        Assert.NotNull(processSpan);

        // receive span carries the operation=receive tag for metrics routing
        Assert.Contains(receiveSpan!.Tags, t => t is { Key: BrighterSemanticConventions.MessagingOperationType, Value: "receive" });

        // process span carries the operation=process tag for metrics routing — feeds messaging.process.duration histogram
        Assert.Contains(processSpan!.Tags, t => t is { Key: BrighterSemanticConventions.MessagingOperationType, Value: "process" });

        // process span inherits producer trace context (parent = producer's traceparent), so handler spans descend from producer
        Assert.Equal(_message.Header.TraceParent?.Value, processSpan.ParentId);

        // receive span is local (parent = pump begin span), not the producer — receive measures broker call only
        Assert.NotEqual(_message.Header.TraceParent?.Value, receiveSpan.ParentId);
    }
}
