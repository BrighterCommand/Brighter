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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

public class MessagePumpTraceIsolationObservabilityTests : IDisposable
{
    private readonly ICollection<Activity> _exportedActivities = new List<Activity>();
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer = new();
    private readonly RoutingKey _routingKey = new("MyTopic");

    public MessagePumpTraceIsolationObservabilityTests()
    {
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    [Fact]
    public void When_Messages_Without_TraceParent_Are_Consumed_Should_Have_Distinct_Traces()
    {
        //Arrange
        var firstMessage = CreateMessageWithoutTraceParent();
        var secondMessage = CreateMessageWithoutTraceParent();

        //Act
        var pumpSpan = _tracer.CreateMessagePumpSpan(
            MessagePumpSpanOperation.Begin,
            _routingKey,
            MessagingSystem.InternalBus);

        var firstReceiveSpan = _tracer.CreateReceiveSpan(_routingKey, MessagingSystem.InternalBus);
        _tracer.EnrichReceiveSpan(firstReceiveSpan, firstMessage);
        _tracer.EndSpan(firstReceiveSpan);
        Assert.Same(pumpSpan, Activity.Current);

        var firstProcessSpan = _tracer.CreateSpan(MessagePumpSpanOperation.Process, firstMessage, MessagingSystem.InternalBus);
        _tracer.EndSpan(firstProcessSpan);
        Assert.Same(pumpSpan, Activity.Current);

        var secondReceiveSpan = _tracer.CreateReceiveSpan(_routingKey, MessagingSystem.InternalBus);
        _tracer.EnrichReceiveSpan(secondReceiveSpan, secondMessage);
        _tracer.EndSpan(secondReceiveSpan);
        Assert.Same(pumpSpan, Activity.Current);

        var secondProcessSpan = _tracer.CreateSpan(MessagePumpSpanOperation.Process, secondMessage, MessagingSystem.InternalBus);
        _tracer.EndSpan(secondProcessSpan);
        Assert.Same(pumpSpan, Activity.Current);

        _tracer.EndSpan(pumpSpan);
        _traceProvider.ForceFlush();

        //Assert
        Assert.Null(firstMessage.Header.TraceParent);
        Assert.Null(secondMessage.Header.TraceParent);

        Assert.NotNull(pumpSpan);
        Assert.NotNull(firstReceiveSpan);
        Assert.NotNull(secondReceiveSpan);
        Assert.NotNull(firstProcessSpan);
        Assert.NotNull(secondProcessSpan);

        Assert.NotEqual(pumpSpan!.TraceId, firstReceiveSpan!.TraceId);
        Assert.NotEqual(pumpSpan.TraceId, firstProcessSpan!.TraceId);
        Assert.NotEqual(firstReceiveSpan.TraceId, secondReceiveSpan!.TraceId);
        Assert.NotEqual(firstProcessSpan.TraceId, secondProcessSpan!.TraceId);

        Assert.Null(firstProcessSpan.ParentId);
        Assert.Null(secondProcessSpan.ParentId);

        Assert.All(_exportedActivities.Where(a => a.DisplayName.EndsWith(MessagePumpSpanOperation.Process.ToSpanName())),
            processSpan => Assert.Null(processSpan.ParentId));
    }

    [Fact]
    public void When_A_Root_Consumer_Span_Is_Ended_Out_Of_Order_Should_Not_Restore_Previous_Activity()
    {
        //Arrange
        var pumpSpan = _tracer.CreateMessagePumpSpan(
            MessagePumpSpanOperation.Begin,
            _routingKey,
            MessagingSystem.InternalBus);

        var receiveSpan = _tracer.CreateReceiveSpan(_routingKey, MessagingSystem.InternalBus);
        using var interveningActivity = new Activity("Intervening").Start();

        //Act
        _tracer.EndSpan(receiveSpan);

        //Assert
        Assert.NotSame(pumpSpan, Activity.Current);

        interveningActivity.Stop();
        Activity.Current = pumpSpan;
        _tracer.EndSpan(pumpSpan);
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
