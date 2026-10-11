using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// Owns an <see cref="IMeterFactory"/> and records every measurement made by meters that factory
/// created, so a test sees only its own sweeper's instruments.
/// </summary>
public sealed class RecordedSweeperMeasurements : IDisposable
{
    public const string TickLag = "paramore.brighter.outbox_sweeper.tick.lag";
    public const string SweepDuration = "paramore.brighter.outbox_sweeper.sweep.duration";
    public const string Sweeps = "paramore.brighter.outbox_sweeper.sweeps";
    public const string Outcome = "paramore.brighter.outbox_sweeper.outcome";

    private readonly ServiceProvider _provider;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<SweeperMeasurement> _measurements = new();

    public RecordedSweeperMeasurements()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        MeterFactory = _provider.GetRequiredService<IMeterFactory>();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, MeterFactory))
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();
    }

    public IMeterFactory MeterFactory { get; }

    public IReadOnlyList<SweeperMeasurement> Of(string instrument) =>
        _measurements.Where(m => m.Instrument == instrument).ToList();

    public bool WaitFor(Func<RecordedSweeperMeasurements, bool> condition, TimeSpan timeout) =>
        SpinWait.SpinUntil(() => condition(this), timeout);

    public void Dispose()
    {
        _listener.Dispose();
        _provider.Dispose();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string? outcome = null;
        foreach (var tag in tags)
            if (tag.Key == Outcome) outcome = tag.Value as string;

        _measurements.Enqueue(new SweeperMeasurement(instrument.Name, value, outcome));
    }
}
