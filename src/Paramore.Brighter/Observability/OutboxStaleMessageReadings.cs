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
using System.Linq;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Holds the latest stale-message readings of an outbox and how many due times ended with each outcome since
/// measurement started.
/// </summary>
/// <remarks>
/// The outbox stale-message monitor is the only writer, and <see cref="OutboxStaleMessageMeter"/> reads it when its
/// instruments are collected. It is public for Brighter's own use, not as an extension point. Every member is safe to
/// call from any thread.
/// </remarks>
public sealed class OutboxStaleMessageReadings
{
    private readonly object _lock = new();
    private long[,] _totals = new long[2, 4];
    private bool _isStarted;
    private string? _outboxType;
    private bool _measuresTopics;
    private int? _latestOverall;
    private IReadOnlyDictionary<RoutingKey, int>? _latestPerTopic;

    /// <summary>Whether measurement has started.</summary>
    public bool IsStarted
    {
        get { lock (_lock) return _isStarted; }
    }

    /// <summary>The type name of the measured outbox, or <c>null</c> before measurement starts.</summary>
    public string? OutboxType
    {
        get { lock (_lock) return _outboxType; }
    }

    /// <summary>Whether the per-topic count runs as well as the overall count.</summary>
    public bool MeasuresTopics
    {
        get { lock (_lock) return _measuresTopics; }
    }

    /// <summary>
    /// Marks measurement as started, with every total at zero.
    /// </summary>
    /// <param name="outboxType">The type name of the measured outbox, such as <c>MsSqlOutbox</c>.</param>
    /// <param name="measuresTopics">Whether the per-topic count runs as well as the overall count.</param>
    public void Start(string outboxType, bool measuresTopics)
    {
        lock (_lock)
        {
            _totals = new long[2, 4];
            _outboxType = outboxType;
            _measuresTopics = measuresTopics;
            _isStarted = true;
        }
    }

    /// <summary>
    /// Records a completed overall count as the latest overall value, and adds one to the overall
    /// <see cref="StaleMeasurementOutcome.Completed"/> total.
    /// </summary>
    /// <param name="staleCount">How many outstanding messages are older than the stale-message age.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="staleCount"/> is negative.</exception>
    public void CompleteOverall(int staleCount)
    {
        if (staleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(staleCount), staleCount, "A stale count cannot be negative.");

        lock (_lock)
        {
            _latestOverall = staleCount;
            _totals[(int)StaleMessageCount.Overall, (int)StaleMeasurementOutcome.Completed]++;
        }
    }

    /// <summary>
    /// Replaces the latest per-topic values with a copy of <paramref name="staleCounts"/>, and adds one to the per-topic
    /// <see cref="StaleMeasurementOutcome.Completed"/> total.
    /// </summary>
    /// <param name="staleCounts">For each measured topic, how many of its outstanding messages are stale.</param>
    /// <exception cref="InvalidOperationException">Topics are not measured, which is also the case before measurement starts.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value in <paramref name="staleCounts"/> is negative.</exception>
    public void CompletePerTopic(IReadOnlyDictionary<RoutingKey, int> staleCounts)
    {
        lock (_lock)
        {
            CheckCanCompletePerTopic(staleCounts);
            _latestPerTopic = staleCounts.ToDictionary(entry => entry.Key, entry => entry.Value);
            _totals[(int)StaleMessageCount.PerTopic, (int)StaleMeasurementOutcome.Completed]++;
        }
    }

    /// <summary>
    /// Records that <paramref name="count"/> failed: clears that count's latest value or values, and adds one to its
    /// <see cref="StaleMeasurementOutcome.Failed"/> total. The other count is left as it was.
    /// </summary>
    /// <param name="count">The stale-message count that failed.</param>
    public void Fail(StaleMessageCount count)
    {
        lock (_lock)
        {
            ClearLatest(count);
            _totals[(int)count, (int)StaleMeasurementOutcome.Failed]++;
        }
    }

    /// <summary>
    /// Records that <paramref name="dueTimes"/> due times of <paramref name="count"/> were skipped: clears that count's
    /// latest value or values, and adds the number to its <see cref="StaleMeasurementOutcome.Skipped"/> total. The other
    /// count is left as it was.
    /// </summary>
    /// <param name="count">The stale-message count whose due times were skipped.</param>
    /// <param name="dueTimes">How many due times were skipped.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dueTimes"/> is less than 1.</exception>
    public void Skip(StaleMessageCount count, int dueTimes)
    {
        if (dueTimes < 1)
            throw new ArgumentOutOfRangeException(nameof(dueTimes), dueTimes, "At least one due time must be skipped.");

        lock (_lock)
        {
            ClearLatest(count);
            _totals[(int)count, (int)StaleMeasurementOutcome.Skipped] += dueTimes;
        }
    }

    /// <summary>
    /// Records that measurement stopped: clears the latest overall value and the latest per-topic values. Every outcome
    /// total is left as it was.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            ClearLatest(StaleMessageCount.Overall);
            ClearLatest(StaleMessageCount.PerTopic);
        }
    }

    /// <summary>
    /// Gets the latest overall value, if there is one.
    /// </summary>
    /// <param name="staleCount">The latest overall value, or zero when there is none.</param>
    /// <returns><c>true</c> when there is a latest overall value; otherwise <c>false</c>.</returns>
    public bool TryGetLatestOverall(out int staleCount)
    {
        lock (_lock)
        {
            staleCount = _latestOverall.GetValueOrDefault();
            return _latestOverall.HasValue;
        }
    }

    /// <summary>
    /// Gets the latest per-topic values, if there are any.
    /// </summary>
    /// <param name="staleCounts">The latest value for each measured topic, or an empty map when there are none.</param>
    /// <returns><c>true</c> when there are latest per-topic values; otherwise <c>false</c>.</returns>
    public bool TryGetLatestPerTopic(out IReadOnlyDictionary<RoutingKey, int> staleCounts)
    {
        lock (_lock)
        {
            staleCounts = _latestPerTopic ?? new Dictionary<RoutingKey, int>();
            return _latestPerTopic is not null;
        }
    }

    /// <summary>
    /// Gets how many due times since measurement started ended with <paramref name="outcome"/> for
    /// <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The stale-message count.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The total, which is zero before any due time has that outcome.</returns>
    public long GetTotal(StaleMessageCount count, StaleMeasurementOutcome outcome)
    {
        lock (_lock)
        {
            return _totals[(int)count, (int)outcome];
        }
    }

    private void CheckCanCompletePerTopic(IReadOnlyDictionary<RoutingKey, int> staleCounts)
    {
        if (!_measuresTopics)
            throw new InvalidOperationException("The per-topic count cannot complete when topics are not measured.");
        if (staleCounts.Any(entry => entry.Value < 0))
            throw new ArgumentOutOfRangeException(nameof(staleCounts), "A stale count cannot be negative.");
    }

    private void ClearLatest(StaleMessageCount count)
    {
        if (count == StaleMessageCount.Overall)
            _latestOverall = null;
        else
            _latestPerTopic = null;
    }
}
