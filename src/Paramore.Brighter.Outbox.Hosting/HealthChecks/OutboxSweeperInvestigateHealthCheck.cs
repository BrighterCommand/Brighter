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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.Hosting.HealthChecks;

/// <summary>
/// An investigate remedy check that reports Degraded when consecutive sweeps have failed, when topics are
/// circuit-broken, or when the tripped topics cannot be read.
/// </summary>
public partial class OutboxSweeperInvestigateHealthCheck : IHealthCheck
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<OutboxSweeperInvestigateHealthCheck>();

    /// <summary>The name of the condition reported while consecutive sweeps have failed.</summary>
    public const string ConsecutiveFailedSweeps = "consecutive_failed_sweeps";

    /// <summary>The name of the condition reported while an active sweeper has circuit-broken topics.</summary>
    public const string TrippedTopics = "tripped_topics";

    /// <summary>The name of the condition reported when the tripped topics could not be read.</summary>
    public const string TrippedTopicsUnreadable = "tripped_topics_unreadable";

    private const int MaxNamedTopics = 10;

    private readonly OutboxSweeperState? _state;
    private readonly int _timerIntervalSeconds;
    private readonly int _failureThreshold;
    private readonly Func<IAmAnOutboxCircuitBreaker?>? _findCircuitBreaker;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxSweeperInvestigateHealthCheck"/> class.
    /// </summary>
    /// <param name="state">The sweeper's shared record, or null when no sweeper is registered.</param>
    /// <param name="options">The sweeper's options, or null to use the defaults.</param>
    /// <param name="findCircuitBreaker">Looks up the circuit breaker in use, or null when there is none.</param>
    /// <param name="failureThreshold">How many consecutive failed sweeps raise the condition.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="failureThreshold"/> is zero or negative.</exception>
    public OutboxSweeperInvestigateHealthCheck(OutboxSweeperState? state, TimedOutboxSweeperOptions? options, Func<IAmAnOutboxCircuitBreaker?>? findCircuitBreaker = null, int failureThreshold = 3)
    {
        if (failureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), failureThreshold, "The failure threshold must be at least one.");

        _state = state;
        options ??= new TimedOutboxSweeperOptions();
        _timerIntervalSeconds = options.TimerInterval;
        _failureThreshold = failureThreshold;
        _findCircuitBreaker = findCircuitBreaker;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_state is null)
            return Task.FromResult(HealthCheckResult.Healthy("No outbox sweeper is registered.", Data(Array.Empty<string>())));

        var snapshot = _state.Read();
        var conditions = new List<(string Name, string Description, Dictionary<string, object> Values)>();

        if (snapshot.ConsecutiveFailedSweeps >= _failureThreshold)
            conditions.Add(FailedSweepsCondition(snapshot.ConsecutiveFailedSweeps));

        if (snapshot.IsActive)
        {
            try
            {
                var names = TrippedTopicNames();
                if (names.Length > 0)
                    conditions.Add(TrippedTopicsCondition(names));
            }
            catch (Exception e)
            {
                Log.TrippedTopicsUnreadable(s_logger, e);
                conditions.Add(TrippedTopicsUnreadableCondition(e));
            }
        }

        if (conditions.Count == 0)
        {
            var healthy = HealthCheckResult.Healthy($"No outbox sweeper condition needs investigation: fewer than {_failureThreshold} consecutive failed sweeps.", Data(Array.Empty<string>()));
            return Task.FromResult(healthy);
        }

        var data = Data(conditions.Select(condition => condition.Name).ToArray());
        foreach (var condition in conditions)
            data[condition.Name] = condition.Values;
        var description = string.Join("; ", conditions.Select(condition => condition.Description));
        return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, description, null, data));
    }

    private string[] TrippedTopicNames()
    {
        var tripped = _findCircuitBreaker?.Invoke()?.TrippedTopics;
        if (tripped is null) return Array.Empty<string>();

        return tripped.Select(topic => topic.Value).OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private (string Name, string Description, Dictionary<string, object> Values) FailedSweepsCondition(int failed)
    {
        var values = new Dictionary<string, object>
        {
            ["count"] = failed,
            ["threshold"] = _failureThreshold,
            ["timer_interval_seconds"] = (double)_timerIntervalSeconds
        };
        var interval = _timerIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        var description = $"{failed} consecutive outbox sweeps have failed (threshold {_failureThreshold}, TimerInterval {interval}s).";
        return (ConsecutiveFailedSweeps, description, values);
    }

    private static (string Name, string Description, Dictionary<string, object> Values) TrippedTopicsCondition(string[] names)
    {
        var values = new Dictionary<string, object>
        {
            ["count"] = names.Length,
            ["topics"] = names
        };
        var listed = string.Join(", ", names.Take(MaxNamedTopics));
        var named = names.Length > MaxNamedTopics
            ? $"{listed} and {names.Length - MaxNamedTopics} more ({names.Length} in total)"
            : listed;
        var description = $"{names.Length} topics are circuit-broken and are not being swept until their cooldown ends: {named}.";
        return (TrippedTopics, description, values);
    }

    private static (string Name, string Description, Dictionary<string, object> Values) TrippedTopicsUnreadableCondition(Exception e)
    {
        var type = e.GetType().FullName ?? e.GetType().Name;
        var values = new Dictionary<string, object> { ["exception_type"] = type };
        var description = $"The tripped topics could not be read from the circuit breaker ({type}).";
        return (TrippedTopicsUnreadable, description, values);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "The outbox sweeper health check could not read the tripped topics from the circuit breaker")]
        public static partial void TrippedTopicsUnreadable(ILogger logger, Exception e);
    }

    private static Dictionary<string, object> Data(string[] conditions) => new()
    {
        [RemedyCheckConvention.RemedyKindKey] = RemedyCheckConvention.Name(RemedyKind.Investigate),
        [RemedyCheckConvention.ConditionsKey] = conditions
    };
}
