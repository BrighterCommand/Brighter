using System;
using System.Collections.Generic;
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
    private static readonly HashSet<string> V1Columns = new(StringComparer.Ordinal)
    {
        "CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"
    };

    public BoxType BoxType => BoxType.Inbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = SpannerInboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.InBoxTableName,
            configuration.SchemaName,
            BoxType.Inbox,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await SpannerBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.InBoxTableName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await SpannerBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.InBoxTableName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await SpannerBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await SpannerPayloadModeValidator.ValidateAsync(
            connection, configuration.InBoxTableName,
            "CommandBody", configuration.BinaryMessagePayload, cancellationToken);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await SpannerBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }
}
