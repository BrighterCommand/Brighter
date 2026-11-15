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

public class MessagePumpLinkObservabilityTests : IDisposable
{
    private readonly ICollection<Activity> _exportedActivities = new List<Activity>();
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer = new();
    private readonly RoutingKey _routingKey = new("MyTopic");

    public MessagePumpLinkObservabilityTests()
    {
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    [Test]
    public async Task When_a_root_consumer_span_is_created_should_link_the_message_pump_span()
    {
        //Arrange
        var message = CreateMessageWithoutTraceParent();

        //Act
        var pumpSpan = _tracer.CreateMessagePumpSpan(
            MessagePumpSpanOperation.Begin,
            _routingKey,
            MessagingSystem.InternalBus);

        var receiveSpan = _tracer.CreateReceiveSpan(_routingKey, MessagingSystem.InternalBus);
        _tracer.EnrichReceiveSpan(receiveSpan, message);
        _tracer.EndSpan(receiveSpan);

        var processSpan = _tracer.CreateSpan(MessagePumpSpanOperation.Process, message, MessagingSystem.InternalBus);
        _tracer.EndSpan(processSpan);

        _tracer.EndSpan(pumpSpan);
        _traceProvider.ForceFlush();

        //Assert
        await Assert.That(message.Header.TraceParent).IsNull();
        await Assert.That(pumpSpan).IsNotNull();
        await Assert.That(receiveSpan).IsNotNull();
        await Assert.That(processSpan).IsNotNull();

        // both root spans stay isolated (their own trace) but link back to the pump
        await Assert.That(receiveSpan!.TraceId).IsNotEqualTo(pumpSpan!.TraceId);
        await Assert.That(processSpan!.TraceId).IsNotEqualTo(pumpSpan.TraceId);

        await Assert.That((receiveSpan.Links).Any(link => link.Context == pumpSpan.Context)).IsTrue();
        await Assert.That((processSpan.Links).Any(link => link.Context == pumpSpan.Context)).IsTrue();
    }

    public void Dispose()
    {
        _traceProvider.Dispose();
        _tracer.Dispose();
    }

    private Message CreateMessageWithoutTraceParent()
    {
        return new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT),
            new MessageBody("{}"));
    }
}
