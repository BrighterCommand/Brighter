using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

[NotInParallel]
public class BrighterTracerEndSpanTimeProviderTests
{
    private readonly TracerProvider _traceProvider;
    private readonly Activity _parentActivity;

    public BrighterTracerEndSpanTimeProviderTests()
    {
        _traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .Build();

        _parentActivity = new ActivitySource("Paramore.Brighter.Tests")
            .StartActivity("BrighterTracerEndSpanTimeProviderTests");
    }

    [Test]
    public async Task When_Ending_A_Span_Duration_Reflects_TimeProvider()
    {
        //arrange
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var tracer = new BrighterTracer(fakeTime);
        var command = new MyCommand { Value = "duration" };

        //act
        var span = tracer.CreateSpan(
            CommandProcessorSpanOperation.Send,
            command,
            _parentActivity,
            options: InstrumentationOptions.All);

        fakeTime.Advance(TimeSpan.FromSeconds(5));

        tracer.EndSpan(span);

        //assert
        await Assert.That(span).IsNotNull();
        await Assert.That(span!.Duration).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [After(Test)]
    public void Cleanup()
    {
        _parentActivity.Dispose();
        _traceProvider.Dispose();
    }
}
