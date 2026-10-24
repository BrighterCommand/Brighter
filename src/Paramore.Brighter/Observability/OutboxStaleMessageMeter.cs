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
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Reports how many outstanding outbox messages are older than the stale-message age, overall and for each measured
/// topic, and how each measurement ended, from the <see cref="OutboxStaleMessageReadings"/> the stale-message monitor
/// writes.
/// </summary>
public sealed class OutboxStaleMessageMeter
{
    private static readonly StaleMessageCount[] s_counts = [StaleMessageCount.Overall, StaleMessageCount.PerTopic];

    private static readonly StaleMeasurementOutcome[] s_outcomes =
    [
        StaleMeasurementOutcome.Completed,
        StaleMeasurementOutcome.Failed,
        StaleMeasurementOutcome.Abandoned,
        StaleMeasurementOutcome.Skipped
    ];

    private readonly OutboxStaleMessageReadings _readings = null!;

    /// <summary>
    /// Creates the stale-message instruments on the Brighter meter, unless there are no readings to report.
    /// </summary>
    /// <param name="meterFactory">Creates the <see cref="BrighterSemanticConventions.MeterName"/> meter.</param>
    /// <param name="readings">
    /// The readings the instruments report. When <c>null</c>, the stale-message monitor is not in use and no instrument
    /// is created.
    /// </param>
    public OutboxStaleMessageMeter(IMeterFactory meterFactory, OutboxStaleMessageReadings? readings = null)
    {
        if (readings is null)
            return;

        _readings = readings;
        var meter = meterFactory.Create(BrighterSemanticConventions.MeterName);

        meter.CreateObservableGauge(
            name: BrighterSemanticConventions.OutboxStaleMessages,
            observeValues: ObserveNothing,
            unit: "{message}",
            description: "Latest number of outstanding outbox messages older than the stale-message age.");

        meter.CreateObservableGauge(
            name: BrighterSemanticConventions.OutboxStaleTopicMessages,
            observeValues: ObserveNothing,
            unit: "{message}",
            description: "Latest number of outstanding outbox messages older than the stale-message age, by topic.");

        meter.CreateObservableCounter(
            name: BrighterSemanticConventions.OutboxStaleMeasurements,
            observeValues: ObserveMeasurements,
            unit: "{measurement}",
            description: "Number of stale-message measurement due times since measurement started, by count and outcome.");
    }

    private static IEnumerable<Measurement<long>> ObserveNothing() => [];

    private IEnumerable<Measurement<long>> ObserveMeasurements()
    {
        if (!_readings.IsStarted)
            yield break;

        var outboxType = _readings.OutboxType;
        var measuresTopics = _readings.MeasuresTopics;
        foreach (var count in s_counts)
        {
            if (count == StaleMessageCount.PerTopic && !measuresTopics)
                continue;

            foreach (var outcome in s_outcomes)
            {
                yield return new Measurement<long>(
                    _readings.GetTotal(count, outcome),
                    new KeyValuePair<string, object?>(BrighterSemanticConventions.OutboxImplementation, outboxType),
                    new KeyValuePair<string, object?>(BrighterSemanticConventions.OutboxStaleCount, ToTagValue(count)),
                    new KeyValuePair<string, object?>(BrighterSemanticConventions.OutboxStaleOutcome, ToTagValue(outcome)));
            }
        }
    }

    private static string ToTagValue(StaleMessageCount count) => count switch
    {
        StaleMessageCount.Overall => "overall",
        StaleMessageCount.PerTopic => "per_topic",
        _ => throw new ArgumentOutOfRangeException(nameof(count), count, null)
    };

    private static string ToTagValue(StaleMeasurementOutcome outcome) => outcome switch
    {
        StaleMeasurementOutcome.Completed => "completed",
        StaleMeasurementOutcome.Failed => "failed",
        StaleMeasurementOutcome.Abandoned => "abandoned",
        StaleMeasurementOutcome.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}
