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
/// A restart remedy check that reports <see cref="MissingConsumers"/> while a running dispatcher has fewer consumers than expected.
/// </summary>
public class ConsumerRestartHealthCheck : IHealthCheck
{
    /// <summary>The name of the condition reported while a running dispatcher has fewer consumers than expected.</summary>
    public const string MissingConsumers = "missing_consumers";

    private readonly IDispatcher? _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerRestartHealthCheck"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher whose consumers are monitored, or null when none is registered.</param>
    public ConsumerRestartHealthCheck(IDispatcher? dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_dispatcher is null)
            return Task.FromResult(HealthCheckResult.Healthy("No dispatcher is registered.", Data(Array.Empty<string>())));

        var snapshot = ((Dispatcher)_dispatcher).GetLifecycleSnapshot();
        if (snapshot.State is DispatcherState.DS_NOTREADY or DispatcherState.DS_AWAITING || snapshot.EndRequested)
            return Task.FromResult(HealthCheckResult.Healthy("The dispatcher is not running, so missing consumers are not reported.", Data(Array.Empty<string>(), snapshot)));

        var running = snapshot.Consumers
            .GroupBy(consumer => consumer.Subscription.Name)
            .ToDictionary(group => group.Key, group => group.Count());
        var counts = snapshot.ExpectedConsumers
            .Select(expected => (Name: expected.Key, Running: running.GetValueOrDefault(expected.Key), Expected: expected.Value))
            .ToArray();
        var runningTotal = counts.Sum(count => count.Running);
        var expectedTotal = counts.Sum(count => count.Expected);

        if (runningTotal >= expectedTotal)
            return Task.FromResult(HealthCheckResult.Healthy($"All {expectedTotal} expected consumers are running.", Data(Array.Empty<string>(), snapshot)));

        var missing = counts.Where(count => count.Running != count.Expected).ToArray();
        var data = Data([MissingConsumers], snapshot);
        data[MissingConsumers] = new Dictionary<string, object>
        {
            ["running_total"] = runningTotal,
            ["expected_total"] = expectedTotal,
            ["subscriptions"] = missing.Select(count => new Dictionary<string, object>
            {
                ["subscription"] = count.Name.Value,
                ["running"] = count.Running,
                ["expected"] = count.Expected
            }).ToArray()
        };
        var description = string.Join("; ", missing.Select(count => $"{count.Name} has {count.Running} of {count.Expected} expected consumers"));
        var status = runningTotal == 0 ? HealthStatus.Unhealthy : HealthStatus.Degraded;
        return Task.FromResult(new HealthCheckResult(status, description, null, data));
    }

    private static Dictionary<string, object> Data(string[] conditions) => new()
    {
        [RemedyCheckConvention.RemedyKindKey] = RemedyCheckConvention.Name(RemedyKind.Restart),
        [RemedyCheckConvention.ConditionsKey] = conditions
    };

    private static Dictionary<string, object> Data(string[] conditions, DispatcherLifecycleSnapshot snapshot)
    {
        var data = Data(conditions);
        data["consumer_operations_in_flight"] = snapshot.ConsumerOperationsInFlight;
        return data;
    }
}
