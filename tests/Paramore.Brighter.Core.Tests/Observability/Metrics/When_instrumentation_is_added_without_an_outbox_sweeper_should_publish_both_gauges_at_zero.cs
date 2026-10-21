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


using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.Extensions.Diagnostics;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class InstrumentationWithoutSweeperGaugesTests
{
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public void When_instrumentation_is_added_without_an_outbox_sweeper_should_publish_both_gauges_at_zero()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        var published = new List<string>();
        var recorded = new Dictionary<string, List<double>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory)) return;
            if (instrument.Name != RunningTime && instrument.Name != TrippedTopics) return;
            published.Add(instrument.Name);
            meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Record(recorded, instrument, value));
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Record(recorded, instrument, value));

        //Act
        using var meterProvider = provider.GetRequiredService<MeterProvider>();
        listener.Start();
        listener.RecordObservableInstruments();

        //Assert
        Assert.Contains(RunningTime, published);
        Assert.Contains(TrippedTopics, published);
        Assert.Equal([0.0], recorded[RunningTime]);
        Assert.Equal([0.0], recorded[TrippedTopics]);
    }

    private static void Record(Dictionary<string, List<double>> recorded, Instrument instrument, double value)
    {
        if (!recorded.TryGetValue(instrument.Name, out var values))
            recorded[instrument.Name] = values = [];
        values.Add(value);
    }
}
