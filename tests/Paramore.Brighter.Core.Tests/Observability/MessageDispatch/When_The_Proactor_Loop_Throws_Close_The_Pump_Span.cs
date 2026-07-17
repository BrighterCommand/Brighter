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
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

public class ProactorLoopThrowsPumpSpanObservabilityTests
{
    private const string ChannelName = "myChannel";
    private readonly RoutingKey _routingKey = new("MyTopic");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IAmAMessagePump _messagePump;
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;

    public ProactorLoopThrowsPumpSpanObservabilityTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();

        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsync(new Dictionary<string, string>()));

        var tracer = new BrighterTracer(_timeProvider);
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

        //a channel whose ReceiveAsync returns null drives the pump down the "message is null" branch,
        //which throws out of the receive loop (Proactor.cs:190)
        var channel = new NullReturningChannelAsync(
            new(ChannelName), _routingKey,
            new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        _messagePump = new Proactor(commandProcessor, (message) => typeof(MyEvent),
            messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel, tracer, instrumentationOptions)
        {
            Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), EmptyChannelDelay = TimeSpan.FromMilliseconds(1000)
        };
    }

    [Test]
    public async Task When_the_proactor_loop_throws_close_the_pump_span()
    {
        //Act - the null message forces the pump to throw out of its receive loop
        Assert.Throws<Exception>(() => _messagePump.Run());

        _traceProvider.ForceFlush();

        //Assert - despite the throw, the begin (pump) span must still be ended and exported, not leaked
        await Assert.That(_exportedActivities.Any(a =>
            a.DisplayName == $"{_routingKey} {MessagePumpSpanOperation.Begin.ToSpanName()}")).IsTrue();
    }
}
