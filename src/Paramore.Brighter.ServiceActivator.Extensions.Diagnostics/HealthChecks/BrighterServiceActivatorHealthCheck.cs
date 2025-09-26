using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

public class BrighterServiceActivatorHealthCheck : IHealthCheck
{
    private readonly IDispatcher _dispatcher;

    public BrighterServiceActivatorHealthCheck(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        var expectedConsumers = ((Dispatcher)_dispatcher).Subscriptions.Sum(c => c.NoOfPerformers);
        var activeConsumers = _dispatcher.Consumers.Count();

        if (expectedConsumers != activeConsumers)
        {
            var status = activeConsumers > 0 ? HealthStatus.Degraded : HealthStatus.Unhealthy;

            return Task.FromResult(new HealthCheckResult(status,
                GenerateUnhealthyMessage()));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"{activeConsumers} healthy consumers."));
    }

    private string GenerateUnhealthyMessage()
    {
        var config = ((Dispatcher)_dispatcher).Subscriptions;

        var unhealthyHosts = new List<string>();
        foreach (var cfg in config)
        {
            var sub = _dispatcher.Consumers.Where(c => c.Subscription.Name == cfg.Name).ToArray();
            if (sub.Length != cfg.NoOfPerformers)
                unhealthyHosts.Add($"{cfg.Name} has {sub.Length} of {cfg.NoOfPerformers} expected consumers");
        }

        return string.Join(";", unhealthyHosts);
    }
}
