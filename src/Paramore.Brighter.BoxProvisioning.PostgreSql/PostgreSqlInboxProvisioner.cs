using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Provisions a PostgreSQL inbox table.
/// </summary>
public class PostgreSqlInboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.Ordinal)
    {
        "commandid", "commandtype", "commandbody", "timestamp", "contextkey"
    };

    public BoxType BoxType => BoxType.Inbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = PostgreSqlInboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.InBoxTableName,
            configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "public";

        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await PostgreSqlBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await PostgreSqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.InBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await PostgreSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "public";

        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await PostgreSqlPayloadModeValidator.ValidateAsync(
            connection, configuration.InBoxTableName, schemaName,
            "commandbody", configuration.BinaryMessagePayload, cancellationToken);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        NpgsqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await PostgreSqlBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, schemaName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }
}
