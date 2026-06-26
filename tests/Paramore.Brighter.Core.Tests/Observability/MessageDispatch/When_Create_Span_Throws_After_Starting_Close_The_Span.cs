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
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch;

public class CreateSpanThrowsAfterStartingObservabilityTests : IDisposable
{
    private readonly ICollection<Activity> _exportedActivities = new List<Activity>();
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer = new();
    private readonly RoutingKey _routingKey = new("MyTopic");

    public CreateSpanThrowsAfterStartingObservabilityTests()
    {
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
    }

    [Fact]
    public void When_create_span_throws_after_starting_close_the_span()
    {
        //Arrange - a correlation id whose value is rejected by Baggage validation makes CreateSpan throw
        //at the baggage step, which runs AFTER the consumer activity has already been started
        var message = new Message(
            new MessageHeader(Id.Random(), _routingKey, MessageType.MT_EVENT, correlationId: new Id("bad=value")),
            new MessageBody("{}"));

        //Act
        Assert.Throws<ArgumentException>(() =>
            _tracer.CreateSpan(MessagePumpSpanOperation.Process, message, MessagingSystem.InternalBus));

        _traceProvider.ForceFlush();

        //Assert - the activity was started before the throw, so it must be ended and exported, not leaked
        Assert.Contains(_exportedActivities, a =>
            a.DisplayName == $"{_routingKey} {MessagePumpSpanOperation.Process.ToSpanName()}");
    }

    public void Dispose()
    {
        _traceProvider.Dispose();
        _tracer.Dispose();
    }
}
