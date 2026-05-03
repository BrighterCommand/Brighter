using System;
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
/// <remarks>
/// Per ADR 0057 §6 the Spanner runner is degenerate (fresh-only) — no V_k chain.
/// On a fresh install it executes the current builder DDL and stamps history at
/// <see cref="VLatestOutbox"/> / <see cref="VLatestInbox"/> (chosen by <see cref="BoxType"/>)
/// under an <c>IsMigrationAppliedAsync</c> gate. Existing-table paths are reworked in
/// later spec 0027 phase 5 tasks.
/// </remarks>
public class SpannerBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration) : IAmABoxMigrationRunner
{
    // Spanner rejects identifiers starting with `_` (reserved for system objects),
    // so this backend uses `BrighterMigrationHistory` while other backends use
    // `__BrighterMigrationHistory`.
    internal const string MigrationHistoryTable = "BrighterMigrationHistory";

    internal const int VLatestOutbox = 7;
    internal const int VLatestInbox = 2;

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureHistoryTableAsync(connection, cancellationToken);

        var vLatest = LatestVersionFor(boxType);

        if (!tableState.TableExists)
        {
            await FreshInstallAsync(connection, tableName, migrations, vLatest, cancellationToken);
            return;
        }

        await BootstrapExistingTableAsync(connection, tableName, migrations, tableState, cancellationToken);
        await ApplyPendingMigrationsAsync(connection, tableName, migrations, tableState, cancellationToken);
    }

    private static int LatestVersionFor(BoxType boxType) => boxType switch
    {
        BoxType.Outbox => VLatestOutbox,
        BoxType.Inbox => VLatestInbox,
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unsupported box type")
    };

    private static async Task FreshInstallAsync(
        SpannerConnection connection, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, int vLatest,
        CancellationToken cancellationToken)
    {
        // Phase-0.3 bridge: V1.UpScript holds the current builder DDL. Task 5.3 deletes
        // the migrations bridge entirely and reads the DDL from configuration directly.
        var builderDdl = migrations[0].UpScript;
        await ExecuteDdlSafeAsync(connection, builderDdl, cancellationToken);

        if (await IsMigrationAppliedAsync(connection, tableName, vLatest, cancellationToken))
            return;

        await InsertHistoryRowAsync(
            connection, tableName, vLatest, $"fresh install at V{vLatest}", cancellationToken);
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

            await InsertHistoryRowAsync(
                connection, tableName, migration.Version, migration.Description, cancellationToken);
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
            await InsertHistoryRowAsync(
                connection, tableName, migration.Version, migration.Description, cancellationToken);
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
        int version, string description,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateInsertCommand(
            MigrationHistoryTable,
            new SpannerParameterCollection
            {
                { "MigrationVersion", SpannerDbType.Int64, version },
                { "BoxTableName", SpannerDbType.String, tableName },
                { "Description", SpannerDbType.String, description },
                { "AppliedAt", SpannerDbType.Timestamp, SpannerParameter.CommitTimestamp }
            });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
