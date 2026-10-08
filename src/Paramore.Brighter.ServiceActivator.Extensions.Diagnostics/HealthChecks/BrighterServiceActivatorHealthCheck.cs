#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

namespace Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Reports consumer availability and persistent failures to receive from message channels.
/// </summary>
public class BrighterServiceActivatorHealthCheck : IHealthCheck
{
    private readonly IDispatcher _dispatcher;
    private readonly int _channelFailureThreshold;

    /// <summary>
    /// Creates a health check that reports degraded health after three consecutive channel failures.
    /// </summary>
    /// <param name="dispatcher">The dispatcher whose consumers are monitored.</param>
    public BrighterServiceActivatorHealthCheck(IDispatcher dispatcher) : this(dispatcher, 3)
    {
    }

    /// <summary>
    /// Creates a health check with a configurable threshold for consecutive channel failures.
    /// </summary>
    /// <param name="dispatcher">The dispatcher whose consumers are monitored.</param>
    /// <param name="channelFailureThreshold">The positive number of consecutive receive failures required to report degraded health.</param>
    /// <exception cref="ArgumentOutOfRangeException">The failure threshold is less than one.</exception>
    public BrighterServiceActivatorHealthCheck(IDispatcher dispatcher, int channelFailureThreshold)
    {
        if (channelFailureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(channelFailureThreshold), "The channel failure threshold must be positive.");

        _dispatcher = dispatcher;
        _channelFailureThreshold = channelFailureThreshold;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        var subscriptions = ((Dispatcher)_dispatcher).Subscriptions.ToArray();
        var expectedConsumers = subscriptions.Sum(c => c.NoOfPerformers);
        var consumers = _dispatcher.Consumers.ToArray();
        var activeConsumers = consumers.Length;
        var failures = consumers
            .Select(consumer => (Consumer: consumer, Count:
                (consumer.Performer as IHaveAChannelFailureCount)?.ConsecutiveChannelFailures ?? 0))
            .Where(failure => failure.Count >= _channelFailureThreshold)
            .ToArray();

        if (expectedConsumers != activeConsumers || failures.Length > 0)
        {
            var status = activeConsumers > 0 ? HealthStatus.Degraded : HealthStatus.Unhealthy;
            var descriptions = new List<string>();
            foreach (var subscription in subscriptions)
            {
                var count = consumers.Count(c => c.Subscription.Name == subscription.Name);
                if (count != subscription.NoOfPerformers)
                    descriptions.Add($"{subscription.Name} has {count} of {subscription.NoOfPerformers} expected consumers");
            }

            descriptions.AddRange(failures.Select(failure =>
                $"{failure.Consumer.Subscription.Name} consumer {failure.Consumer.Name} has {failure.Count} consecutive channel failures"));

            return Task.FromResult(new HealthCheckResult(status, string.Join(";", descriptions)));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"{activeConsumers} healthy consumers."));
    }
}
