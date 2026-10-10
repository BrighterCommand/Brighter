using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.Tasks.TestDoubles;

/// <summary>
/// Owns an <see cref="IMeterFactory"/> and, on <see cref="Observe"/>, collects the confirmation queue depth from
/// meters that factory created, so a test sees only its own meter's instrument.
/// </summary>
public sealed class ObservedQueueDepths : IDisposable
{
    public const string Instrument = "paramore.brighter.publish_confirmation.queue.depth";

    private readonly ServiceProvider _provider;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<QueueDepth> _depths = new();

    public ObservedQueueDepths()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        MeterFactory = _provider.GetRequiredService<IMeterFactory>();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, MeterFactory) && instrument.Name == Instrument)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) => Record(value, tags));
        _listener.Start();
    }

    public IMeterFactory MeterFactory { get; }

    /// <summary>Collects the instrument now and returns what it reported.</summary>
    public IReadOnlyList<QueueDepth> Observe()
    {
        _depths.Clear();
        _listener.RecordObservableInstruments();
        return _depths.ToList();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _provider.Dispose();
    }

    private void Record(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string? system = null, destination = null;
        foreach (var tag in tags)
        {
            if (tag.Key == "messaging.system") system = tag.Value as string;
            if (tag.Key == "messaging.destination.name") destination = tag.Value as string;
        }

        _depths.Enqueue(new QueueDepth(system, destination, value));
    }
}
