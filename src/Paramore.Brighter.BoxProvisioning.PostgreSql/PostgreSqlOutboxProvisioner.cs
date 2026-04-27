using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Provisions a PostgreSQL outbox table.
/// </summary>
public class PostgreSqlOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.Ordinal)
    {
        "id", "messageid", "topic", "messagetype", "timestamp", "correlationid",
        "replyto", "contenttype", "partitionkey", "workflowid", "jobid", "dispatched",
        "headerbag", "body", "source", "type", "dataschema", "subject",
        "traceparent", "tracestate", "baggage", "dataref", "specversion"
    };

    public BoxType BoxType => BoxType.Outbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = PostgreSqlOutboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.OutBoxTableName,
            configuration.SchemaName,
            BoxType.Outbox,
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
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await PostgreSqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.OutBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await PostgreSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "public";

        using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await PostgreSqlPayloadModeValidator.ValidateAsync(
            connection, configuration.OutBoxTableName, schemaName,
            "body", configuration.BinaryMessagePayload, cancellationToken);
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
