using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Grpc.Core;
using Paramore.Brighter.Inbox.Spanner;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Runs box migrations against a Spanner database. DDL operations use
/// <c>CreateDdlCommand</c> and are separate from read-write transactions.
/// Spanner handles DDL concurrency internally — no advisory lock is needed.
/// "Already exists" errors on DDL are caught for crash safety.
/// </summary>
/// <remarks>
/// Per ADR 0057 §6 the Spanner runner is degenerate (fresh-only) — no V_k chain,
/// so the <c>migrations</c> parameter on <see cref="MigrateAsync"/> is ignored.
/// Three paths:
/// <list type="bullet">
///   <item><description>Fresh install: executes the live builder DDL and stamps history at <c>V_latest</c> under an <c>IsMigrationAppliedAsync</c> gate.</description></item>
///   <item><description>Existing table without history (bootstrap): verifies the discriminator column (<c>HeaderBag</c> for outbox / <c>CommandBody</c> for inbox); absence throws <see cref="ConfigurationException"/>; presence stamps <c>V_latest</c> with the ADR §6 "no known legacy installations" description (A-2).</description></item>
///   <item><description>Existing table with history (normal): compares <c>MAX(V)</c> to <c>V_latest</c>; equality is a no-op; <c>MAX(V) &gt; V_latest</c> throws <see cref="ConfigurationException"/>; <c>MAX(V) &lt; V_latest</c> is undefined per ADR §6 (manual recovery required) and throws the same out-of-sync error.</description></item>
/// </list>
/// <para>
/// TOCTOU strategy: unlike the relational runners (MSSQL/PostgreSQL/MySQL/SQLite) which re-detect
/// table and history state under an advisory lock, this runner consumes the caller-supplied
/// <see cref="BoxTableState"/> directly. TOCTOU protection is provided by three Spanner-native
/// mechanisms: (a) Spanner serializes DDL internally so concurrent <c>CREATE TABLE</c> calls do
/// not interleave; (b) <c>ExecuteDdlSafeAsync</c> swallows <c>AlreadyExists</c> /
/// <c>FailedPrecondition</c> on DDL replay (crash safety); (c) the history insert is gated by
/// <c>IsMigrationAppliedAsync</c> against the PK <c>(BoxTableName, MigrationVersion)</c>, so a
/// racing process cannot double-stamp. No application-level lock is therefore required.
/// </para>
/// </remarks>
public class SpannerBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration) : IAmABoxMigrationRunner
{
    // Spanner rejects identifiers starting with `_` (reserved for system objects),
    // so this backend uses `BrighterMigrationHistory` while other backends use
    // `__BrighterMigrationHistory`.
    internal const string MigrationHistoryTable = "BrighterMigrationHistory";

    // IMPORTANT: keep these in sync with the relational chain length —
    //   VLatestOutbox === MySqlOutboxMigrations.All(...).Count
    //                 === MsSqlOutboxMigrations.All(...).Count
    //                 === PostgreSqlOutboxMigrations.All(...).Count
    //                 === SqliteOutboxMigrations.All(...).Count
    //   VLatestInbox  === <Backend>InboxMigrations.All(...).Count (across all four relational backends)
    // Spanner has no V_k chain (ADR 0057 §6 — fresh-install-only), so the latest version is
    // effectively a stamp on a freshly-built table. When a relational backend advances to
    // V8/V3 etc., bump these constants so Spanner's history row keeps the same V_latest as
    // its relational siblings — the per-backend drift tests in
    // tests/Paramore.Brighter.Spanner.Tests/BoxProvisioning will fail otherwise.
    internal const int VLatestOutbox = 7;
    internal const int VLatestInbox = 2;

    private const string BootstrapDescription =
        "bootstrap: spanner-assumed-current (no known legacy installations, A-2)";

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = migrations; // Spanner is fresh-install-only — no V_k chain (ADR 0057 §6).
        _ = schemaName; // Spanner does not use schemas; the configuration's database is implicit.

        using var connection = SpannerConnectionHelper.CreateConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureHistoryTableAsync(connection, cancellationToken);

        var vLatest = LatestVersionFor(boxType);

        if (!tableState.TableExists)
        {
            await FreshInstallAsync(connection, tableName, boxType, vLatest, cancellationToken);
            return;
        }

        if (!tableState.HistoryExists)
        {
            await BootstrapExistingTableAsync(connection, tableName, boxType, vLatest, cancellationToken);
            return;
        }

        RunNormalPath(tableName, vLatest, tableState.CurrentVersion);
    }

    private static int LatestVersionFor(BoxType boxType) => boxType switch
    {
        BoxType.Outbox => VLatestOutbox,
        BoxType.Inbox => VLatestInbox,
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unsupported box type")
    };

    private async Task FreshInstallAsync(
        SpannerConnection connection, string tableName, BoxType boxType, int vLatest,
        CancellationToken cancellationToken)
    {
        var builderDdl = BuildBoxDdl(boxType, tableName);
        await ExecuteDdlSafeAsync(connection, builderDdl, cancellationToken);

        if (await IsMigrationAppliedAsync(connection, tableName, vLatest, cancellationToken))
            return;

        // The IsMigrationAppliedAsync check above + this insert form a TOCTOU pair: two
        // concurrent fresh-installing replicas can both pass the existence check at zero
        // (commit timestamps from the winner's eventual insert have not yet propagated to
        // the loser's read snapshot — Spanner's history-row insert uses
        // SpannerParameter.CommitTimestamp, so visibility lags), then both attempt the
        // insert; the loser hits AlreadyExists on the PK (BoxTableName, MigrationVersion).
        // Mirror ExecuteDdlSafeAsync's filter shape and absorb the benign race.
        try
        {
            await InsertHistoryRowAsync(
                connection, tableName, vLatest, $"fresh install at V{vLatest}", cancellationToken);
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.AlreadyExists)
        {
            // Concurrent fresh-installer already stamped V_latest — benign race.
        }
    }

    private string BuildBoxDdl(BoxType boxType, string tableName) => boxType switch
    {
        BoxType.Outbox => SpannerOutboxBuilder.GetDDL(tableName, configuration.BinaryMessagePayload),
        BoxType.Inbox => SpannerInboxBuilder.GetDDL(tableName),
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unsupported box type")
    };

    private static async Task BootstrapExistingTableAsync(
        SpannerConnection connection, string tableName, BoxType boxType, int vLatest,
        CancellationToken cancellationToken)
    {
        var columns = await SpannerBoxDetectionHelpers.GetTableColumnsAsync(
            connection, tableName, cancellationToken);

        var discriminator = SpannerBoxDetectionHelpers.DiscriminatorFor(boxType);
        if (!columns.Contains(discriminator))
        {
            throw new ConfigurationException(
                $"Table '{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (await IsMigrationAppliedAsync(connection, tableName, vLatest, cancellationToken))
            return;

        await InsertHistoryRowAsync(
            connection, tableName, vLatest, BootstrapDescription, cancellationToken);
    }

    private static void RunNormalPath(string tableName, int vLatest, int currentVersion)
    {
        if (currentVersion == vLatest) return;

        throw new ConfigurationException(
            $"Migration list out of sync for table '{tableName}': " +
            $"installed V={currentVersion}, expected V={vLatest}. " +
            "Manual recovery required per ADR 0057 §6.");
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
