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

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Publishes an outbox sweeper's health on the <see cref="BrighterSemanticConventions.MeterName"/> meter:
/// how late each sweep started, how long each sweep took, and how many sweeps ended each way. See ADR 0081.
/// </summary>
/// <remarks>
/// The instruments are recorded directly, not derived from spans, so they report every sweep even when
/// traces are sampled out.
/// </remarks>
public sealed class SweeperMeter : IAmABrighterSweeperMeter
{
    private readonly Histogram<double> _tickLag;
    private readonly Histogram<double> _sweepDuration;
    private readonly Counter<long> _sweeps;
    private readonly OutboxSweeperState? _state;
    private readonly Func<IAmAnOutboxCircuitBreaker?>? _findCircuitBreaker;

    /// <summary>
    /// Creates the sweeper's instruments on the Brighter meter.
    /// </summary>
    /// <param name="meterFactory">Creates the <see cref="BrighterSemanticConventions.MeterName"/> meter.</param>
    /// <param name="state">What the sweeper is doing, read by the gauges. When null, the gauges report zero.</param>
    /// <param name="findCircuitBreaker">Finds the outbox circuit breaker, if one is registered.</param>
    public SweeperMeter(
        IMeterFactory meterFactory,
        OutboxSweeperState? state = null,
        Func<IAmAnOutboxCircuitBreaker?>? findCircuitBreaker = null)
    {
        _state = state;
        _findCircuitBreaker = findCircuitBreaker;

        var meter = meterFactory.Create(BrighterSemanticConventions.MeterName);

        _tickLag = meter.CreateHistogram<double>(
            name: BrighterSemanticConventions.OutboxSweeperTickLag,
            unit: "s",
            description: "How long after its due time an outbox sweep started.");

        _sweepDuration = meter.CreateHistogram<double>(
            name: BrighterSemanticConventions.OutboxSweeperSweepDuration,
            unit: "s",
            description: "How long an outbox sweep took.");

        _sweeps = meter.CreateCounter<long>(
            name: BrighterSemanticConventions.OutboxSweeperSweeps,
            unit: "{sweep}",
            description: "Number of outbox sweeps, by how each ended.");

        meter.CreateObservableGauge(
            name: BrighterSemanticConventions.OutboxSweeperSweepRunningTime,
            observeValue: ObserveRunningTime,
            unit: "s",
            description: "How long the sweep in progress has been running, or 0 when none is.");

        meter.CreateObservableGauge(
            name: BrighterSemanticConventions.OutboxSweeperTrippedTopics,
            observeValue: ObserveTrippedTopics,
            unit: "{topic}",
            description: "Number of topics whose outbox circuit breaker is tripped.");
    }

    /// <inheritdoc />
    public void RecordTickLag(TimeSpan lag) =>
        _tickLag.Record(Math.Max(0, lag.TotalSeconds));

    /// <inheritdoc />
    public void RecordSweep(SweepOutcome outcome, TimeSpan duration)
    {
        var outcomeTag = new KeyValuePair<string, object?>(BrighterSemanticConventions.OutboxSweeperOutcome, ToTagValue(outcome));
        _sweepDuration.Record(duration.TotalSeconds, outcomeTag);
        _sweeps.Add(1, outcomeTag);
    }

    private double ObserveRunningTime() => _state is null ? 0 : _state.Read().RunningTime.TotalSeconds;

    private long ObserveTrippedTopics()
    {
        if (_state is null) return 0;
        var breaker = _findCircuitBreaker?.Invoke();
        return breaker is null ? 0 : breaker.TrippedTopics.Count();
    }

    private static string ToTagValue(SweepOutcome outcome) => outcome switch
    {
        SweepOutcome.Completed => "completed",
        SweepOutcome.Failed => "failed",
        SweepOutcome.LockUnavailable => "lock_unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}
