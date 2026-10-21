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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Outbox.Hosting;

/// <summary>
/// Opts a host in to the outbox sweeper's remedy health checks.
/// </summary>
public static class OutboxSweeperHealthChecksBuilderExtensions
{
    /// <summary>
    /// Registers the outbox sweeper's restart and investigate health checks.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="hangThreshold">How long a sweep may run before it is reported hung, or null for the default.</param>
    /// <param name="failureThreshold">How many consecutive failed sweeps raise the failure condition.</param>
    /// <returns>The builder.</returns>
    public static IHealthChecksBuilder AddBrighterOutboxSweeperHealthChecks(this IHealthChecksBuilder builder, TimeSpan? hangThreshold = null, int failureThreshold = 3)
    {
        return builder
            .Add(new HealthCheckRegistration(
                "brighter-outbox-sweeper-restart",
                sp => new OutboxSweeperRestartHealthCheck(sp.GetService<OutboxSweeperState>(), sp.GetService<TimedOutboxSweeperOptions>(), hangThreshold),
                failureStatus: null,
                tags: new[] { RemedyCheckConvention.RestartTag }))
            .Add(new HealthCheckRegistration(
                "brighter-outbox-sweeper-investigate",
                sp => new OutboxSweeperInvestigateHealthCheck(sp.GetService<OutboxSweeperState>(), sp.GetService<TimedOutboxSweeperOptions>(), () => sp.GetService<IAmAnOutboxCircuitBreaker>(), failureThreshold),
                failureStatus: null,
                tags: new[] { RemedyCheckConvention.InvestigateTag }));
    }
}
