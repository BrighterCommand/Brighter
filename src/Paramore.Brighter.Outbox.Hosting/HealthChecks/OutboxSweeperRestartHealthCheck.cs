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
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.Hosting.HealthChecks;

/// <summary>
/// A restart remedy check that reports Degraded with <see cref="HungSweep"/> while an outbox sweep has run
/// past the hang threshold. Only a restart of the process clears a hung sweep.
/// </summary>
public class OutboxSweeperRestartHealthCheck : IHealthCheck
{
    /// <summary>The name of the condition reported while a sweep is hung.</summary>
    public const string HungSweep = "hung_sweep";

    private readonly OutboxSweeperState? _state;
    private readonly TimeSpan _timerInterval;
    private readonly TimeSpan _hangThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxSweeperRestartHealthCheck"/> class.
    /// </summary>
    /// <param name="state">The sweeper's shared record, or null when no sweeper is registered.</param>
    /// <param name="options">The sweeper's options, or null to use the defaults.</param>
    /// <param name="hangThreshold">How long a sweep may run before it is hung, or null for three times the timer interval.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="hangThreshold"/> is supplied and is zero or negative.</exception>
    public OutboxSweeperRestartHealthCheck(OutboxSweeperState? state, TimedOutboxSweeperOptions? options, TimeSpan? hangThreshold = null)
    {
        if (hangThreshold <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(hangThreshold), hangThreshold, "The hang threshold must be greater than zero.");

        _state = state;
        options ??= new TimedOutboxSweeperOptions();
        _timerInterval = TimeSpan.FromSeconds(options.TimerInterval);
        _hangThreshold = hangThreshold ?? TimeSpan.FromTicks(_timerInterval.Ticks * 3);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_state is null)
            return Task.FromResult(Healthy("No outbox sweeper is registered."));

        var runningTime = _state.Read().RunningTime;
        if (runningTime <= _hangThreshold)
        {
            return Task.FromResult(Healthy($"No outbox sweep is hung (threshold {Seconds(_hangThreshold)}s)."));
        }

        RemedyCondition[] conditions = [HungSweepCondition(runningTime)];
        var data = RemedyCheckConvention.Data(RemedyKind.Restart, conditions);
        return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, RemedyCheckConvention.Describe(conditions), null, data));
    }

    private RemedyCondition HungSweepCondition(TimeSpan runningTime)
    {
        var values = new Dictionary<string, object>
        {
            ["running_time_seconds"] = runningTime.TotalSeconds,
            ["hang_threshold_seconds"] = _hangThreshold.TotalSeconds,
            ["timer_interval_seconds"] = _timerInterval.TotalSeconds
        };
        var description = $"Outbox sweep has been running for {Seconds(runningTime)}s, longer than the {Seconds(_hangThreshold)}s threshold (TimerInterval {Seconds(_timerInterval)}s).";
        return new RemedyCondition(HungSweep, description, values);
    }

    private static HealthCheckResult Healthy(string description) =>
        HealthCheckResult.Healthy(description, RemedyCheckConvention.Data(RemedyKind.Restart));

    private static string Seconds(TimeSpan value) => value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}
