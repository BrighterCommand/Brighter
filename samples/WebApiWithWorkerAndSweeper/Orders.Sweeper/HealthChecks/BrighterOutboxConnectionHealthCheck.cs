using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter;
using Paramore.Brighter.MsSql;

namespace Orders.Sweeper.HealthChecks;

public class BrighterOutboxConnectionHealthCheck : IHealthCheck
{
    private readonly IAmARelationalDbConnectionProvider _connectionProvider;

    public BrighterOutboxConnectionHealthCheck(IAmARelationalDbConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            await connection.OpenAsync(cancellationToken);

            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            var command = connection.CreateCommand();
            if (_connectionProvider.HasOpenTransaction) command.Transaction = _connectionProvider.GetTransaction();

            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);

            if (!_connectionProvider.IsSharedConnection) connection.Dispose();
            else if (!_connectionProvider.HasOpenTransaction) connection.Close();

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
