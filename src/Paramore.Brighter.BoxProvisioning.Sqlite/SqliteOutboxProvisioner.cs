using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Provisions a SQLite outbox table.
/// </summary>
public class SqliteOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        "MessageId", "MessageType", "Topic", "Timestamp", "CorrelationId",
        "ReplyTo", "ContentType", "PartitionKey", "WorkflowId", "JobId", "Dispatched",
        "HeaderBag", "Body", "Source", "Type", "DataSchema", "Subject",
        "TraceParent", "TraceState", "Baggage", "DataRef", "SpecVersion"
    };

    public BoxType BoxType => BoxType.Outbox;
    public string BoxTableName => configuration.OutBoxTableName;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = SqliteOutboxMigrations.All(configuration);
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
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await SqliteBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.OutBoxTableName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await SqliteBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.OutBoxTableName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, configuration.OutBoxTableName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await SqliteBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.OutBoxTableName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await SqlitePayloadModeValidators.ValidateAsync(
            connection, configuration.OutBoxTableName,
            "Body", configuration.BinaryMessagePayload, cancellationToken);
    }

    private static async Task<int> DetectCurrentVersionAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await SqliteBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, cancellationToken);
        if (actualColumns.IsSupersetOf(V1Columns)) return 1;
        return 0;
    }
}
