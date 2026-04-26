using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Provisions a MSSQL outbox table. Creates the table if it doesn't exist,
/// detects existing tables, and applies migrations.
/// </summary>
public class MsSqlOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "MessageId", "Topic", "MessageType", "Timestamp", "CorrelationId",
        "ReplyTo", "ContentType", "PartitionKey", "WorkflowId", "JobId", "Dispatched",
        "HeaderBag", "Body", "Source", "Type", "DataSchema", "Subject",
        "TraceParent", "TraceState", "Baggage", "DataRef", "SpecVersion"
    };

    public BoxType BoxType => BoxType.Outbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MsSqlOutboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.OutBoxTableName,
            configuration.SchemaName,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await MsSqlBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await MsSqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.OutBoxTableName, schemaName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await MsSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MsSqlPayloadModeValidator.ValidateAsync(
            connection, configuration.OutBoxTableName, schemaName,
            "Body", configuration.BinaryMessagePayload, cancellationToken);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        SqlConnection connection, string tableName, string schemaName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await MsSqlBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, schemaName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }
}
