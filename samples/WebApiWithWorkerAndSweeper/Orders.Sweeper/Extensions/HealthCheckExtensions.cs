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
            sp =>
            {
                var connProvider = sp.GetService<IAmARelationalDbConnectionProvider>();
                if (connProvider == null)
                    throw new ConfigurationException("No connection provider found for Brighter Outbox health check.");
                return new BrighterOutboxConnectionHealthCheck(connProvider);
            },
            HealthStatus.Unhealthy,
            null,
            TimeSpan.FromSeconds(15)));
    }
}
