using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.ServiceActivator.Extensions.HealthChecks;

public class BrighterServiceActivatorHealthCheck : IHealthCheck
{
    private readonly IDispatcher _dispatcher;

    public BrighterServiceActivatorHealthCheck(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        var expectedConsumers = ((Dispatcher)_dispatcher).Connections.Sum(c => c.NoOfPeformers);
        var activeConsumers = _dispatcher.Consumers.Count();

        if (expectedConsumers != activeConsumers)
        {
            var status = HealthStatus.Degraded;
            if (activeConsumers < 1)
            {
                status = HealthStatus.Unhealthy;
            }

            return Task.FromResult(new HealthCheckResult(status,
                GenerateUnhealthyMessage()));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"{activeConsumers} healthy consumers."));
    }

    private string GenerateUnhealthyMessage()
    {
        var config = ((Dispatcher)_dispatcher).Connections;

        var unhealthyHosts = new List<string>();
        foreach (var cfg in config)
        {
            var sub = _dispatcher.Consumers.Where(c => c.SubscriptionName == cfg.Name).ToArray();
            if(sub.Count() != cfg?.NoOfPeformers)
                unhealthyHosts.Add($"{cfg.Name} has {sub.Count()} of {cfg.NoOfPeformers} expected consumers");
        }

        return string.Join(';', unhealthyHosts);
    }
}
