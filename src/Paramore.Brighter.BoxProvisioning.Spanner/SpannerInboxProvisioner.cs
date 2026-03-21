using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Provisions a Spanner inbox table.
/// </summary>
public class SpannerInboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    public BoxType BoxType => BoxType.Inbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = SpannerInboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        await migrationRunner.MigrateAsync(
            configuration.InBoxTableName,
            configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await SpannerOutboxProvisioner.DoesTableExistAsync(
            connection, configuration.InBoxTableName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await SpannerOutboxProvisioner.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(connection, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await SpannerOutboxProvisioner.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task<int> DetectCurrentVersionAsync(
        SpannerConnection connection, CancellationToken cancellationToken)
    {
        var hasCommandBody = await SpannerOutboxProvisioner.ColumnExistsAsync(
            connection, configuration.InBoxTableName, "CommandBody", cancellationToken);
        return hasCommandBody ? 1 : 0;
    }
}
