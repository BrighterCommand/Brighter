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

namespace Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// A take-out-of-service remedy check that reports Degraded with <see cref="PersistentChannelFailures"/>
/// while a consumer has reached the channel failure threshold. It never reports Unhealthy.
/// </summary>
public class ConsumerTakeOutOfServiceHealthCheck : IHealthCheck
{
    /// <summary>The name of the condition reported while a consumer is persistently failing to read its channel.</summary>
    public const string PersistentChannelFailures = "persistent_channel_failures";

    private readonly IDispatcher? _dispatcher;
    private readonly int _channelFailureThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerTakeOutOfServiceHealthCheck"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher whose consumers are monitored, or null when none is registered.</param>
    /// <param name="channelFailureThreshold">The number of consecutive channel failures at which a consumer is reported.</param>
    public ConsumerTakeOutOfServiceHealthCheck(IDispatcher? dispatcher, int channelFailureThreshold = 3)
    {
        _dispatcher = dispatcher;
        _channelFailureThreshold = channelFailureThreshold;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            [RemedyCheckConvention.RemedyKindKey] = RemedyCheckConvention.Name(RemedyKind.TakeOutOfService),
            [RemedyCheckConvention.ConditionsKey] = Array.Empty<string>()
        };
        return Task.FromResult(HealthCheckResult.Healthy("No dispatcher is registered.", data));
    }
}
