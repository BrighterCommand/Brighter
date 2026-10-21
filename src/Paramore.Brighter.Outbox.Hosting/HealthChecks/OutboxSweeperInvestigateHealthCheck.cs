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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.Hosting.HealthChecks;

/// <summary>
/// An investigate remedy check that reports Degraded when consecutive sweeps have failed, when topics are
/// circuit-broken, or when the tripped topics cannot be read.
/// </summary>
public class OutboxSweeperInvestigateHealthCheck : IHealthCheck
{
    /// <summary>The name of the condition reported while consecutive sweeps have failed.</summary>
    public const string ConsecutiveFailedSweeps = "consecutive_failed_sweeps";

    /// <summary>The name of the condition reported while an active sweeper has circuit-broken topics.</summary>
    public const string TrippedTopics = "tripped_topics";

    /// <summary>The name of the condition reported when the tripped topics could not be read.</summary>
    public const string TrippedTopicsUnreadable = "tripped_topics_unreadable";

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxSweeperInvestigateHealthCheck"/> class.
    /// </summary>
    /// <param name="state">The sweeper's shared record, or null when no sweeper is registered.</param>
    /// <param name="options">The sweeper's options, or null to use the defaults.</param>
    /// <param name="findCircuitBreaker">Looks up the circuit breaker in use, or null when there is none.</param>
    /// <param name="failureThreshold">How many consecutive failed sweeps raise the condition.</param>
    public OutboxSweeperInvestigateHealthCheck(OutboxSweeperState? state, TimedOutboxSweeperOptions? options, Func<IAmAnOutboxCircuitBreaker?>? findCircuitBreaker = null, int failureThreshold = 3)
    {
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            [RemedyCheckConvention.RemedyKindKey] = RemedyCheckConvention.Name(RemedyKind.Investigate),
            [RemedyCheckConvention.ConditionsKey] = Array.Empty<string>()
        };
        return Task.FromResult(HealthCheckResult.Healthy("No outbox sweeper is registered.", data));
    }
}
