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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Opts a host in to the consumer remedy health checks.
/// </summary>
public static class ConsumerHealthChecksBuilderExtensions
{
    /// <summary>
    /// Registers the consumer take-out-of-service and restart health checks.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="channelFailureThreshold">The number of consecutive channel failures at which a consumer is reported.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channelFailureThreshold"/> is zero or negative.</exception>
    public static IHealthChecksBuilder AddBrighterConsumerHealthChecks(this IHealthChecksBuilder builder, int channelFailureThreshold = 3)
    {
        if (channelFailureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(channelFailureThreshold), channelFailureThreshold, "The channel failure threshold must be greater than zero.");

        return builder
            .Add(new HealthCheckRegistration(
                "brighter-consumer-take-out-of-service",
                sp => new ConsumerTakeOutOfServiceHealthCheck(sp.GetService<IDispatcher>(), channelFailureThreshold),
                failureStatus: HealthStatus.Degraded,
                tags: new[] { RemedyCheckConvention.TakeOutOfServiceTag }))
            .Add(new HealthCheckRegistration(
                "brighter-consumer-restart",
                sp => new ConsumerRestartHealthCheck(sp.GetService<IDispatcher>()),
                failureStatus: HealthStatus.Degraded,
                tags: new[] { RemedyCheckConvention.RestartTag }));
    }
}
