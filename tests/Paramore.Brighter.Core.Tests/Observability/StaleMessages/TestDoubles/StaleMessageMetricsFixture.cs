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
using OpenTelemetry.Metrics;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// Builds a container with <c>AddBrighterInstrumentation</c>, resolves its <see cref="MeterProvider"/> so the
/// instrumentation creates its meters, and listens to the <see cref="BrighterSemanticConventions.MeterName"/>
/// instruments that the container's <see cref="IMeterFactory"/> created.
/// </summary>
public sealed class StaleMessageMetricsFixture : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<string, byte> _published = new();
    private readonly ConcurrentQueue<StaleMeasurement> _measurements = new();

    /// <summary>Builds the container, creates its meters and starts listening to them.</summary>
    /// <param name="configure">Registers what the test needs in the container before it is built.</param>
    public StaleMessageMetricsFixture(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        configure(services);
        _services = services.BuildServiceProvider();

        var meterFactory = _services.GetRequiredService<IMeterFactory>();
        _services.GetRequiredService<MeterProvider>();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Meter.Name != BrighterSemanticConventions.MeterName)
                return;
            _published[instrument.Name] = 0;
            listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument.Name, value, tags));
        _listener.Start();
    }

    /// <summary>The names of the Brighter instruments the container's meter factory created.</summary>
    public IReadOnlyCollection<string> PublishedInstruments => _published.Keys.ToArray();

    /// <summary>Collects the instruments now and returns every measurement they reported.</summary>
    public IReadOnlyList<StaleMeasurement> Observe()
    {
        _measurements.Clear();
        _listener.RecordObservableInstruments();
        return _measurements.ToList();
    }

    /// <summary>Stops the listener and disposes the container.</summary>
    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }

    private void Record(string instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        _measurements.Enqueue(new StaleMeasurement(instrument, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
}
