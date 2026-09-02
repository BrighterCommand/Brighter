#region Licence

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class SendSpanMetricsTests : IDisposable
{
    private readonly TracerProvider _traceProvider;
    private readonly BrighterTracer _tracer;
    private readonly SpyMessagingMeter _messagingMeter;
    private readonly BrighterMetricsFromTracesProcessor _processor;
    private readonly Activity _parentActivity;

    public SendSpanMetricsTests()
    {
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(new List<Activity>())
            .Build();

        _tracer = new BrighterTracer();
        _messagingMeter = new SpyMessagingMeter();
        _processor = new BrighterMetricsFromTracesProcessor(_tracer, new DisabledDbMeter(), _messagingMeter);
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("SendSpanMetricsTests")!;
    }

    [Fact]
    public void When_ending_a_send_span_should_still_record_a_client_operation()
    {
        //arrange
        var sendSpan = _tracer.CreateSpan(
            CommandProcessorSpanOperation.Send,
            new MyCommand(),
            _parentActivity,
            options: InstrumentationOptions.All);
        sendSpan.Stop();

        //act
        _processor.OnEnd(sendSpan);

        //assert
        Assert.Equal(1, _messagingMeter.RecordClientOperationCallCount);
    }

    public void Dispose()
    {
        _parentActivity.Dispose();
        _traceProvider.Dispose();
    }
}
