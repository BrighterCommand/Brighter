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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class When_the_gauges_are_observed_after_many_sweeps_and_topics_should_keep_a_fixed_attribute_set
{
    private const string GaugePrefix = "paramore.brighter.outbox_sweeper.";
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";
    private const int Sweeps = 1000;
    private const int Topics = 100;

    [Fact]
    public void When_1000_sweeps_trip_100_topics_should_publish_one_empty_attribute_set_per_gauge()
    {
        //Arrange
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var state = new OutboxSweeperState(new FakeTimeProvider());
        var breaker = new InMemoryOutboxCircuitBreaker();
        var meter = new SweeperMeter(meterFactory, state, () => breaker);
        var attributeSets = new Dictionary<string, HashSet<string>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory)) return;
            if (instrument.Name != RunningTime && instrument.Name != TrippedTopics) return;
            attributeSets[instrument.Name] = [];
            l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            attributeSets[instrument.Name].Add(Canonical(tags)));
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            attributeSets[instrument.Name].Add(Canonical(tags)));
        listener.Start();

        //Act
        for (var sweep = 0; sweep < Sweeps; sweep++)
        {
            state.SweepStarted();
            state.LockObtained();
            if (sweep % (Sweeps / Topics) == 0) breaker.TripTopic(new RoutingKey($"topic-{sweep}"));
            listener.RecordObservableInstruments();
            state.SweepEnded(SweepOutcome.Completed);
            listener.RecordObservableInstruments();
        }

        GC.KeepAlive(meter);

        //Assert
        Assert.Equal(Topics, breaker.TrippedTopics.Count());
        Assert.Equal([RunningTime, TrippedTopics], attributeSets.Keys.OrderBy(name => name).ToArray());
        Assert.All(attributeSets.Keys, name => Assert.StartsWith(GaugePrefix, name));
        Assert.All(attributeSets.Values, sets => Assert.Equal([string.Empty], sets.ToArray()));
    }

    private static string Canonical(ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        string.Join(",", tags.ToArray().Select(tag => $"{tag.Key}={tag.Value}").OrderBy(tag => tag, StringComparer.Ordinal));
}
