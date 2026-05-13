#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Runs box migrations against a SQLite database. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Per ADR 0057 §4 /
/// ADR 0058 §B.1, SQLite has no advisory-lock primitive — <c>BEGIN IMMEDIATE</c> atomically
/// opens the transaction and reserves the database-wide writer slot, so
/// <see cref="SqliteProvisioningUnitOfWork"/> takes no <c>IAmA*AdvisoryLock</c>.
/// </summary>
/// <remarks>
/// <para>
/// SQLite's grammar lacks <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>, so each V2..V_latest
/// migration carries an <see cref="IAmABoxMigration.IdempotencyCheckSql"/> probing
/// <c>pragma_table_info</c>. The runner evaluates this scalar before applying
/// <see cref="IAmABoxMigration.UpScript"/> and skips the ALTER (still inserting the history row)
/// when the column is already present.
/// </para>
/// <para>
/// The whole bootstrap/normal chain runs inside the single BEGIN IMMEDIATE transaction (per
/// ADR 0057 §5a — SQLite is whole-chain transactional like MSSQL/Postgres). A mid-chain failure
/// rolls everything back, including the history table itself if <c>EnsureHistoryTableAsync</c>
/// ran inside the same transaction.
/// </para>
/// <para>
/// <strong>WAL journal mode side effect:</strong> when <paramref name="enableWalMode"/> is
/// <see langword="true"/> (the default for new deployments), the runner issues
/// <c>PRAGMA journal_mode=WAL</c> at the start of every <see cref="MigrateAsync"/> call.
/// This pragma is database-file-wide and affects every other connection — a database that
/// was deliberately configured for DELETE or TRUNCATE journal mode will be silently switched
/// to WAL. Pass <see langword="false"/> to leave the existing journal mode untouched if the
/// host application owns that decision.
/// </para>
/// <para>
/// <strong>Writer-slot contention:</strong> <see cref="MigrateAsync"/> honours
/// <paramref name="lockTimeout"/> as the maximum wait for the SQLite writer slot when another
/// writer (Brighter or otherwise) holds it. The runner sets
/// <c>SqliteConnection.DefaultTimeout</c> to the lockTimeout (rounded up to whole seconds —
/// the driver's granularity), and Microsoft.Data.Sqlite drives sqlite3_busy_timeout from that
/// value on every internal statement, including the synthesised <c>BEGIN IMMEDIATE</c>. The
/// driver's built-in busy handler retries with backoff inside the call; we do not wrap a
/// retry loop in C#. If the budget elapses while the slot is still held, the original
/// <see cref="SqliteException"/> with <c>SqliteErrorCode == 5</c> (<c>SQLITE_BUSY</c>)
/// propagates to the runner's catch path. Note: <c>PRAGMA busy_timeout</c> issued in SQL is
/// silently overwritten by the next command's CommandTimeout-derived re-application, so
/// DefaultTimeout is the only reliable hook.
/// </para>
/// </remarks>
public class SqliteBoxMigrationRunner : SqlBoxMigrationRunner<SqliteConnection, SqliteTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";

    private readonly bool _enableWalMode;
    private readonly TimeSpan _lockTimeout;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public SqliteBoxMigrationRunner(
        SqliteBoxDetectionHelper detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        bool enableWalMode = true)
        : base(detectionHelper, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<SqliteBoxMigrationRunner>())
    {
        _enableWalMode = enableWalMode;
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises a default
    /// <see cref="SqliteBoxDetectionHelper"/>; removed when DI cascade lands in Phase 9.
    /// </summary>
    public SqliteBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        bool enableWalMode = true)
        : this(new SqliteBoxDetectionHelper(), configuration, logger: null, lockTimeout: lockTimeout, enableWalMode: enableWalMode)
    {
    }

    /// <summary>
    /// Convenience constructor using the default <see cref="BoxProvisioningOptions.MigrationLockTimeout"/>
    /// of 30 seconds and WAL journal mode enabled. Tests and callers that need a custom timeout
    /// or want to skip the WAL pragma should use the multi-argument form.
    /// </summary>
    public SqliteBoxMigrationRunner(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, TimeSpan.FromSeconds(30))
    {
    }

    // ==== Per-backend hook overrides for SqlBoxMigrationRunner ====

    protected override async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(Configuration.ConnectionString);
        connection.DefaultTimeout = ToDriverBusyTimeoutSeconds(_lockTimeout);
        await connection.OpenAsync(cancellationToken);

        if (_enableWalMode)
        {
            await EnsureWalModeAsync(connection, cancellationToken);
        }

        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<SqliteTransaction>> CreateUnitOfWorkAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<IAmAProvisioningUnitOfWork<SqliteTransaction>>(
            new SqliteProvisioningUnitOfWork(connection, Logger));
    }

    // SQLite has no schema concept, so the schema is folded out of the lock resource. The
    // string is symbolic only — SqliteProvisioningUnitOfWork's BeginAsync logs it for trace
    // diagnostics but does not use it for locking (BEGIN IMMEDIATE owns that role).
    protected override string LockResourceFor(string? schemaName, string tableName)
    {
        _ = schemaName;
        return tableName;
    }

    protected override async Task EnsureHistoryTableAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken)
    {
        _ = schemaName; // SQLite has no schema concept.

        // No race-handling needed: BEGIN IMMEDIATE above acquires SQLite's database-wide RESERVED
        // lock, so only one writer can be inside this transaction at a time. Concurrent runners
        // queue at the BEGIN IMMEDIATE call rather than racing on CREATE TABLE — by the time a
        // second writer enters this method, the first has already committed and IF NOT EXISTS
        // sees the table.
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS [{MIGRATION_HISTORY_TABLE}] (
    [MigrationVersion] INTEGER NOT NULL,
    [BoxTableName] TEXT NOT NULL,
    [Description] TEXT NOT NULL,
    [AppliedAt] TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY ([BoxTableName], [MigrationVersion])
)";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected override async Task RunFreshPathAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        _ = schemaName; // SQLite has no schema concept.

        if (migrations.Count == 0) return;

        // V1's UpScript IS the live builder DDL (V_latest-shape per ADR §3 fresh-install fast
        // path). A list whose first entry is anything other than V1 would silently install the
        // wrong schema, so reject it before any DDL fires.
        if (migrations[0].Version != 1)
            throw new ConfigurationException(
                $"Cannot install '{tableName}' from a fresh state: " +
                $"the first migration must be V1, but the supplied migrations list starts at V{migrations[0].Version}.");

        // Stamp directly at V_latest with a "fresh install" marker — V2..V_latest ALTERs
        // would all be no-ops (and would in fact throw "duplicate column name" without the
        // IdempotencyCheckSql skip), so we elide the chain.
        await ExecuteUpScriptAsync(connection, transaction!, migrations[0].UpScript, cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, transaction!, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    protected override async Task RunBootstrapPathAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        _ = schemaName; // SQLite has no schema concept.

        var detected = await DetectionHelper.DetectCurrentVersionAsync(
            connection, tableName, null, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = DetectionHelper.DiscriminatorFor(boxType);
            throw new ConfigurationException(
                $"Table '{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (detected == 0)
        {
            throw new ConfigurationException(
                $"Table '{tableName}' does not match any known schema version. " +
                $"Cannot bootstrap a Brighter {boxType.ToString().ToLowerInvariant()} from an unrecognised column set.");
        }

        await InsertHistoryRowAsync(
            connection, transaction!, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ApplyOrSkipAsync(connection, transaction!, tableName, migration, cancellationToken);
        }
    }

    protected override async Task RunNormalPathAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        _ = schemaName; // SQLite has no schema concept.

        var maxVersion = await DetectionHelper.GetMaxVersionAsync(
            connection, tableName, null, cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ApplyOrSkipAsync(connection, transaction!, tableName, migration, cancellationToken);
        }
    }

    private static async Task ApplyOrSkipAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, IAmABoxMigration migration,
        CancellationToken cancellationToken)
    {
        // Per ADR §6: if IdempotencyCheckSql is non-null, evaluate it under the lock-bearing
        // transaction. A non-zero scalar means the migration's effect is already present —
        // skip UpScript but still record history so MAX(V) advances.
        if (!string.IsNullOrEmpty(migration.IdempotencyCheckSql))
        {
            var alreadyApplied = await ScalarIsPositiveAsync(
                connection, transaction, migration.IdempotencyCheckSql!, cancellationToken);
            if (!alreadyApplied)
            {
                await ExecuteUpScriptAsync(connection, transaction, migration.UpScript, cancellationToken);
            }
        }
        else
        {
            await ExecuteUpScriptAsync(connection, transaction, migration.UpScript, cancellationToken);
        }

        await InsertHistoryRowAsync(
            connection, transaction, tableName,
            migration.Version, migration.Description, cancellationToken);
    }

    private static async Task<bool> ScalarIsPositiveAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && Convert.ToInt64(result) > 0;
    }

    private static async Task ExecuteUpScriptAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string upScript, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = upScript;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static int ToDriverBusyTimeoutSeconds(TimeSpan lockTimeout)
    {
        // Microsoft.Data.Sqlite drives sqlite3_busy_timeout from connection.DefaultTimeout
        // (in seconds), re-applying it before every internal command — including the BEGIN
        // command synthesised by BeginTransaction(IsolationLevel, deferred). PRAGMA busy_timeout
        // would be silently overwritten by that re-application, so DefaultTimeout is the only
        // reliable way to honour lockTimeout. Driver granularity is whole seconds; sub-second
        // lockTimeouts floor to 1s. DefaultTimeout = 0 means "wait forever" in this driver
        // (not "fail-fast"), so callers wanting fail-fast pass TimeSpan.Zero and get ~1s.
        return (int)Math.Max(1, Math.Ceiling(lockTimeout.TotalSeconds));
    }

    private static async Task EnsureWalModeAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryRowAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, int version, string description,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO [{MIGRATION_HISTORY_TABLE}] ([MigrationVersion], [BoxTableName], [Description])
VALUES (@Version, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
