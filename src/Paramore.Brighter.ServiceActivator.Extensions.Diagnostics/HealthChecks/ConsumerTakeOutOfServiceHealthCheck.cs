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
using System.Linq;
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channelFailureThreshold"/> is zero or negative.</exception>
    public ConsumerTakeOutOfServiceHealthCheck(IDispatcher? dispatcher, int channelFailureThreshold = 3)
    {
        if (channelFailureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(channelFailureThreshold), channelFailureThreshold, "The channel failure threshold must be at least one.");

        _dispatcher = dispatcher;
        _channelFailureThreshold = channelFailureThreshold;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_dispatcher is null)
            return Task.FromResult(Healthy("No dispatcher is registered."));

        var failing = _dispatcher.Consumers
            .Select(consumer => (Consumer: consumer, Count: (consumer.Performer as IHaveAChannelFailureCount)?.ConsecutiveChannelFailures ?? 0))
            .Where(failure => failure.Count >= _channelFailureThreshold)
            .ToArray();

        if (failing.Length == 0)
            return Task.FromResult(Healthy($"The {PersistentChannelFailures} condition is absent: no consumer has {_channelFailureThreshold} consecutive channel failures."));

        RemedyCondition[] conditions = [PersistentChannelFailuresCondition(failing)];
        var data = RemedyCheckConvention.Data(RemedyKind.TakeOutOfService, conditions);
        return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, RemedyCheckConvention.Describe(conditions), null, data));
    }

    private RemedyCondition PersistentChannelFailuresCondition((IAmAConsumer Consumer, int Count)[] failing)
    {
        var values = new Dictionary<string, object>
        {
            ["threshold"] = _channelFailureThreshold,
            ["consumers"] = failing.Select(failure => new Dictionary<string, object>
            {
                ["subscription"] = failure.Consumer.Subscription.Name.Value,
                ["consumer"] = failure.Consumer.Name.Value,
                ["count"] = failure.Count
            }).ToArray()
        };
        var description = string.Join("; ", failing.Select(failure =>
            $"{failure.Consumer.Subscription.Name} consumer {failure.Consumer.Name} has {failure.Count} consecutive channel failures"));
        return new RemedyCondition(PersistentChannelFailures, description, values);
    }

    private static HealthCheckResult Healthy(string description) =>
        HealthCheckResult.Healthy(description, RemedyCheckConvention.Data(RemedyKind.TakeOutOfService));
}
