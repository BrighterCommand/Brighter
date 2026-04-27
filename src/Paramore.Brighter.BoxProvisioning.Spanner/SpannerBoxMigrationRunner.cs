using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Grpc.Core;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Runs box migrations against a Spanner database. DDL operations use
/// <c>CreateDdlCommand</c> and are separate from read-write transactions.
/// Spanner handles DDL concurrency internally — no advisory lock is needed.
/// "Already exists" errors on DDL are caught for crash safety.
/// </summary>
public class SpannerBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration) : IAmABoxMigrationRunner
{
    // Spanner rejects identifiers starting with `_` (reserved for system objects),
    // so this backend uses `BrighterMigrationHistory` while other backends use
    // `__BrighterMigrationHistory`.
    internal const string MigrationHistoryTable = "BrighterMigrationHistory";

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = boxType; // TODO(spec 0027 phase 5): degenerate Spanner runner with discriminator gate
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureHistoryTableAsync(connection, cancellationToken);
        await BootstrapExistingTableAsync(connection, tableName, migrations, tableState, cancellationToken);
        await ApplyPendingMigrationsAsync(connection, tableName, migrations, tableState, cancellationToken);
    }

    private static async Task BootstrapExistingTableAsync(
        SpannerConnection connection, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, BoxTableState tableState,
        CancellationToken cancellationToken)
    {
        if (tableState is not { TableExists: true, HistoryExists: false })
            return;

        foreach (var migration in migrations)
        {
            if (migration.Version > tableState.CurrentVersion) break;

            await InsertHistoryRowAsync(connection, tableName, migration, cancellationToken);
        }
    }

    private static async Task ApplyPendingMigrationsAsync(
        SpannerConnection connection, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, BoxTableState tableState,
        CancellationToken cancellationToken)
    {
        foreach (var migration in migrations)
        {
            if (migration.Version <= tableState.CurrentVersion)
                continue;

            if (await IsMigrationAppliedAsync(connection, tableName, migration.Version, cancellationToken))
                continue;

            await ExecuteDdlSafeAsync(connection, migration.UpScript, cancellationToken);
            await InsertHistoryRowAsync(connection, tableName, migration, cancellationToken);
        }
    }

    private static async Task ExecuteDdlSafeAsync(
        SpannerConnection connection, string ddl,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = connection.CreateDdlCommand(ddl);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.AlreadyExists
                                         || ex.RpcException.StatusCode == StatusCode.FailedPrecondition)
        {
            // Table/column already exists — safe to continue (crash between DDL and history write)
        }
    }

    private static async Task EnsureHistoryTableAsync(
        SpannerConnection connection, CancellationToken cancellationToken)
    {
        const string ddl = $"""
            CREATE TABLE IF NOT EXISTS `{MigrationHistoryTable}` (
                `MigrationVersion` INT64 NOT NULL,
                `BoxTableName` STRING(255) NOT NULL,
                `Description` STRING(MAX) NOT NULL,
                `AppliedAt` TIMESTAMP NOT NULL OPTIONS (allow_commit_timestamp = true)
            ) PRIMARY KEY (`BoxTableName`, `MigrationVersion`)
            """;

        await ExecuteDdlSafeAsync(connection, ddl, cancellationToken);
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        SpannerConnection connection, string tableName,
        int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateSelectCommand(
            $"SELECT COUNT(1) FROM `{MigrationHistoryTable}` WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @Version",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Version", SpannerDbType.Int64, version }
            });

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    private static async Task InsertHistoryRowAsync(
        SpannerConnection connection, string tableName,
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateInsertCommand(
            MigrationHistoryTable,
            new SpannerParameterCollection
            {
                { "MigrationVersion", SpannerDbType.Int64, migration.Version },
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Description", SpannerDbType.String, migration.Description },
                { "AppliedAt", SpannerDbType.Timestamp, SpannerParameter.CommitTimestamp }
            });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
