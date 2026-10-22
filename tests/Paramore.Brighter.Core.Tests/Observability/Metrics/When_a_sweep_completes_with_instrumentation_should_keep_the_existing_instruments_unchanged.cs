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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class ExistingSweeperInstrumentsUnchangedTests
{
    private sealed record Published(string Name, string? Unit, string Kind, HashSet<string> TagKeys);

    [Fact]
    public async Task When_a_sweep_completes_with_instrumentation_should_keep_the_existing_instruments_unchanged()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true);
        services.AddSingleton<IAmAnOutboxProducerMediator>(new SwitchableOutboxProducerMediator());
        services.AddMetrics();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        var instruments = new ConcurrentDictionary<string, Published>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory)) return;
            instruments[instrument.Name] = new Published(instrument.Name, instrument.Unit, instrument.GetType().Name, []);
            meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => RecordTags(instruments, instrument, tags));
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => RecordTags(instruments, instrument, tags));
        listener.Start();
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            //Act
            await sweeper.StartAsync(CancellationToken.None);
            var swept = await WaitForCompletedSweep(state, TimeSpan.FromSeconds(10));
            var recorded = SpinWait.SpinUntil(
                () => instruments.TryGetValue("paramore.brighter.outbox_sweeper.sweeps", out var s) && s.TagKeys.Count > 0
                    && instruments.TryGetValue("paramore.brighter.outbox_sweeper.sweep.duration", out var d) && d.TagKeys.Count > 0
                    && instruments.ContainsKey("paramore.brighter.outbox_sweeper.tick.lag"),
                TimeSpan.FromSeconds(10));
            var options = new OutboxCircuitBreakerOptions();

            //Assert
            Assert.True(swept, "No sweep completed");
            Assert.True(recorded, "The existing sweeper instruments did not record a measurement");

            var tickLag = instruments["paramore.brighter.outbox_sweeper.tick.lag"];
            Assert.Equal("Histogram`1", tickLag.Kind);
            Assert.Equal("s", tickLag.Unit);
            Assert.Empty(tickLag.TagKeys);

            var duration = instruments["paramore.brighter.outbox_sweeper.sweep.duration"];
            Assert.Equal("Histogram`1", duration.Kind);
            Assert.Equal("s", duration.Unit);
            Assert.Equal(["paramore.brighter.outbox_sweeper.outcome"], duration.TagKeys);

            var sweeps = instruments["paramore.brighter.outbox_sweeper.sweeps"];
            Assert.Equal("Counter`1", sweeps.Kind);
            Assert.Equal("{sweep}", sweeps.Unit);
            Assert.Equal(["paramore.brighter.outbox_sweeper.outcome"], sweeps.TagKeys);

            Assert.Equal(10, options.CooldownCount);
        }
        finally
        {
            await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static void RecordTags(
        ConcurrentDictionary<string, Published> instruments,
        Instrument instrument,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (!instruments.TryGetValue(instrument.Name, out var published)) return;
        lock (published.TagKeys)
            foreach (var tag in tags) published.TagKeys.Add(tag.Key);
    }

    private static async Task<bool> WaitForCompletedSweep(OutboxSweeperState state, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (state.Read().LastOutcome == SweepOutcome.Completed) return true;
            await Task.Delay(10);
        }

        return false;
    }
}
