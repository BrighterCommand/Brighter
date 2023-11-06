using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.MsSql;

namespace Orders.Sweeper.HealthChecks;

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
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
