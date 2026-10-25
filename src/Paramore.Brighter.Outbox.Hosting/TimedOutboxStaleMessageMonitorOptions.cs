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

namespace Paramore.Brighter.Outbox.Hosting;

/// <summary>
/// The settings of the outbox stale-message monitor: the stale age, the ceiling of each count, and how often and how
/// long the monitor measures the outbox.
/// </summary>
public sealed class TimedOutboxStaleMessageMonitorOptions
{
    /// <summary>
    /// The largest stale age the monitor accepts: 365 days.
    /// </summary>
    public static readonly TimeSpan MaxStaleMessageAge = TimeSpan.FromDays(365);

    /// <summary>
    /// The largest ceiling the monitor accepts: 1000.
    /// </summary>
    public const int MaxCountCeiling = 1000;

    /// <summary>
    /// The largest measurement interval the monitor accepts: 4,294,967,294 milliseconds, the longest period a timer
    /// accepts.
    /// </summary>
    public static readonly TimeSpan MaxMeasurementInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>
    /// Creates the settings with the given stale age and the default ceiling, interval and timeout.
    /// </summary>
    /// <param name="staleMessageAge">How old a message that has not been dispatched must be to count as stale.</param>
    public TimedOutboxStaleMessageMonitorOptions(TimeSpan staleMessageAge)
    {
        StaleMessageAge = staleMessageAge;
    }

    /// <summary>
    /// How old a message that has not been dispatched must be to count as stale. The overall count uses it, and it is
    /// the stale age of every publication that does not set one of its own. There is no default.
    /// </summary>
    public TimeSpan StaleMessageAge { get; }

    /// <summary>
    /// The largest value one count reports, for the overall count and for each topic in the per-topic count. A value
    /// equal to the ceiling means "the ceiling or more". Defaults to 100.
    /// </summary>
    public int CountCeiling { get; set; } = 100;

    /// <summary>
    /// The time between two measurements of the outbox. Defaults to one minute.
    /// </summary>
    public TimeSpan MeasurementInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The longest a measurement waits for the outbox before the monitor abandons it. Must be less than
    /// <see cref="MeasurementInterval"/>. Defaults to ten seconds.
    /// </summary>
    public TimeSpan MeasurementTimeout { get; set; } = TimeSpan.FromSeconds(10);

    internal void Validate()
    {
        if (StaleMessageAge <= TimeSpan.Zero || StaleMessageAge > MaxStaleMessageAge)
            throw new ConfigurationException(
                $"{nameof(StaleMessageAge)} must be greater than zero and at most {MaxStaleMessageAge}, but was {StaleMessageAge}");
    }
}
