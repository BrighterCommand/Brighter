#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// Owns an <see cref="IMeterFactory"/> and, on <see cref="Observe"/>, collects the three stale-message instruments
/// from meters that factory created, so a test sees only its own meter's instruments.
/// </summary>
public sealed class ObservedStaleMessageInstruments : IDisposable
{
    public const string MESSAGES = "paramore.brighter.outbox.stale.messages";
    public const string TOPIC_MESSAGES = "paramore.brighter.outbox.stale.topic.messages";
    public const string MEASUREMENTS = "paramore.brighter.outbox.stale.measurements";

    private static readonly HashSet<string> s_instruments = [MESSAGES, TOPIC_MESSAGES, MEASUREMENTS];

    private readonly ServiceProvider _provider;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<string, string?> _units = new();
    private readonly ConcurrentQueue<StaleMeasurement> _measurements = new();

    public ObservedStaleMessageInstruments()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        MeterFactory = _provider.GetRequiredService<IMeterFactory>();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, MeterFactory) || !s_instruments.Contains(instrument.Name))
                return;
            _units[instrument.Name] = instrument.Unit;
            listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument.Name, value, tags));
        _listener.Start();
    }

    public IMeterFactory MeterFactory { get; }

    /// <summary>The unit of each stale-message instrument the meter created, by instrument name.</summary>
    public IReadOnlyDictionary<string, string?> Units => _units;

    /// <summary>Collects the instruments now and returns every measurement they reported.</summary>
    public IReadOnlyList<StaleMeasurement> Observe()
    {
        _measurements.Clear();
        _listener.RecordObservableInstruments();
        return _measurements.ToList();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _provider.Dispose();
    }

    private void Record(string instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        _measurements.Enqueue(new StaleMeasurement(instrument, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
}
