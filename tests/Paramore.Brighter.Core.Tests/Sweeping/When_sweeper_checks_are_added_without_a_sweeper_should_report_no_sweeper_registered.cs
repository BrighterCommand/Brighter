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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperChecksWithoutASweeperTests
{
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Test]
    public async Task When_sweeper_checks_are_added_without_a_sweeper_should_report_no_sweeper_registered()
    {
        //Arrange
        var services = SweeperContainer.Create(new InMemoryLock(), new FakeTimeProvider());
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        var recorded = new Dictionary<string, List<double>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory)) return;
            if (instrument.Name != RunningTime && instrument.Name != TrippedTopics) return;
            meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Measure(recorded, instrument, value));
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Measure(recorded, instrument, value));
        listener.SetMeasurementEventCallback<int>((instrument, value, _, _) => Measure(recorded, instrument, value));

        //Act
        HealthReport? report = null;
        var thrown = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            using var meterProvider = provider.GetRequiredService<MeterProvider>();
            listener.Start();
            listener.RecordObservableInstruments();
            report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(CancellationToken.None);
        });

        //Assert
        await Assert.That(thrown).IsNull();
        await Assert.That(report).IsNotNull();
        await AssertNoSweeperRegistered(report.Entries["brighter-outbox-sweeper-restart"], RemedyKind.Restart);
        await AssertNoSweeperRegistered(report.Entries["brighter-outbox-sweeper-investigate"], RemedyKind.Investigate);
        await Assert.That(recorded[RunningTime]).IsEqualTo([0.0]);
        await Assert.That(recorded[TrippedTopics]).IsEqualTo([0.0]);
    }

    private static async Task AssertNoSweeperRegistered(HealthReportEntry entry, RemedyKind kind)
    {
        await Assert.That(entry.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(entry.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        await Assert.That(entry.Description).Contains("no outbox sweeper is registered");
        await Assert.That(entry.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(kind));
        var conditions = await Assert.That(entry.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>();
        await Assert.That(conditions).IsEmpty();
    }

    private static void Measure(Dictionary<string, List<double>> recorded, Instrument instrument, double value)
    {
        if (!recorded.TryGetValue(instrument.Name, out var values))
            recorded[instrument.Name] = values = [];
        values.Add(value);
    }
}
