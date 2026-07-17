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
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

public class PropagatedMessagesKeepLinkingMessagePumpObservabilityTests : IDisposable
{
    private readonly ICollection<Activity> _exportedActivities = new List<Activity>();
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer = new();
    private readonly RoutingKey _routingKey = new("MyTopic");

    public PropagatedMessagesKeepLinkingMessagePumpObservabilityTests()
    {
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    [Test]
    public async Task When_propagated_messages_are_consumed_should_keep_linking_the_message_pump_span()
    {
        //Arrange
        // two consecutive messages, both carrying a producer TraceParent, so each process span
        // descends from a *remote* trace rather than the local pump
        var firstMessage = CreateMessageWithProducerTraceParent();
        var secondMessage = CreateMessageWithProducerTraceParent();

        //Act
        // mirror the pump: a single Begin span wrapping per-message receive + process spans
        var pumpSpan = _tracer.CreateMessagePumpSpan(
            MessagePumpSpanOperation.Begin,
            _routingKey,
            MessagingSystem.InternalBus);

        // iteration 1
        var firstReceiveSpan = _tracer.CreateReceiveSpan(_routingKey, MessagingSystem.InternalBus);
        _tracer.EnrichReceiveSpan(firstReceiveSpan, firstMessage);
        _tracer.EndSpan(firstReceiveSpan);
        var firstProcessSpan = _tracer.CreateSpan(MessagePumpSpanOperation.Process, firstMessage, MessagingSystem.InternalBus);
        _tracer.EndSpan(firstProcessSpan);

        // the pump must remain the ambient span between iterations for the link to be re-established
        var ambientBetweenIterations = Activity.Current;

        // iteration 2
        var secondReceiveSpan = _tracer.CreateReceiveSpan(_routingKey, MessagingSystem.InternalBus);
        _tracer.EnrichReceiveSpan(secondReceiveSpan, secondMessage);
        _tracer.EndSpan(secondReceiveSpan);
        var secondProcessSpan = _tracer.CreateSpan(MessagePumpSpanOperation.Process, secondMessage, MessagingSystem.InternalBus);
        _tracer.EndSpan(secondProcessSpan);

        _tracer.EndSpan(pumpSpan);
        _traceProvider.ForceFlush();

        //Assert
        await Assert.That(pumpSpan).IsNotNull();
        await Assert.That(secondReceiveSpan).IsNotNull();
        await Assert.That(secondProcessSpan).IsNotNull();

        // both messages genuinely carry propagated context
        await Assert.That(firstMessage.Header.TraceParent).IsNotNull();
        await Assert.That(secondMessage.Header.TraceParent).IsNotNull();

        // the pump is restored as Activity.Current once the first message's spans end
        await Assert.That(ambientBetweenIterations).IsEqualTo(pumpSpan);

        // ...so the *second* message's spans still link back to the pump, not just the first
        await Assert.That((secondReceiveSpan!.Links).Any(link => link.Context == pumpSpan!.Context)).IsTrue();
        await Assert.That((secondProcessSpan!.Links).Any(link => link.Context == pumpSpan!.Context)).IsTrue();
    }

    public void Dispose()
    {
        _traceProvider.Dispose();
        _tracer.Dispose();
    }

    private Message CreateMessageWithProducerTraceParent()
    {
        var message = new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("{}"));

        var producerActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("Producer");
        new TextContextPropogator().PropogateContext(producerActivity?.Context, message);
        producerActivity?.Stop();

        return message;
    }
}
