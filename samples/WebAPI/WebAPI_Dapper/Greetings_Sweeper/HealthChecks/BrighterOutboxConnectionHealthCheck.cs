using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter;

namespace Greetings_Sweeper.HealthChecks;

public class BrighterOutboxConnectionHealthCheck : IHealthCheck
{
    private readonly IAmARelationalDbConnectionProvider  _connectionProvider;

    public BrighterOutboxConnectionHealthCheck(IAmARelationalDbConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            var command = connection.CreateCommand();

            command.CommandText = "SELECT 1;";
            var check = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt64(check) == 1 ? 
                HealthCheckResult.Healthy(" The outbox could be reached") : 
                HealthCheckResult.Unhealthy(" The outbox could not be reached");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
