using Greetings_Sweeper.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter;

namespace Greetings_Sweeper.Extensions;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder BrighterOutboxHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration("outbox",
            sp =>
            {
                var connProvider = sp.GetService<IAmARelationalDbConnectionProvider>();
                if (connProvider == null)
                    throw new ConfigurationException("No connection provider found for Brighter Outbox health check.");
                return new BrighterOutboxConnectionHealthCheck(connProvider);
            },
            HealthStatus.Unhealthy,
            ["brighter", "outbox"],
            TimeSpan.FromSeconds(15)));
    }
}
