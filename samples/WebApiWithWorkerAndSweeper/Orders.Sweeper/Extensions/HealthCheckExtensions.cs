using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Sweeper.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.MsSql;

namespace Orders.Sweeper.Extensions;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddBrighterOutbox(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration("Brighter Outbox",
            sp => new BrighterOutboxConnectionHealthCheck(sp.GetService<IAmARelationalDbConnectionProvider>()),
            HealthStatus.Unhealthy,
            null,
            TimeSpan.FromSeconds(15)));
    }
}
