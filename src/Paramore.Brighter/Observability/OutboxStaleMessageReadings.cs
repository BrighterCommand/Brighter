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
}
