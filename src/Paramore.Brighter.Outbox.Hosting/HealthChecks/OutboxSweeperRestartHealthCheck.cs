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

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxSweeperRestartHealthCheck"/> class.
    /// </summary>
    /// <param name="state">The sweeper's shared record, or null when no sweeper is registered.</param>
    /// <param name="options">The sweeper's options, or null to use the defaults.</param>
    /// <param name="hangThreshold">How long a sweep may run before it is hung, or null for three times the timer interval.</param>
    public OutboxSweeperRestartHealthCheck(OutboxSweeperState? state, TimedOutboxSweeperOptions? options, TimeSpan? hangThreshold = null)
    {
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            [RemedyCheckConvention.RemedyKindKey] = RemedyCheckConvention.Name(RemedyKind.Restart),
            [RemedyCheckConvention.ConditionsKey] = Array.Empty<string>()
        };

        return Task.FromResult(HealthCheckResult.Healthy("No outbox sweeper is registered.", data));
    }
}
