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
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Runs box migrations against a SQLite database. Uses <c>BEGIN IMMEDIATE TRANSACTION</c> to
/// acquire the writer slot up front and a runner-owned exponential-backoff retry loop on
/// <c>SQLITE_BUSY</c> bounded by <see cref="_lockTimeout"/> (per ADR 0057 §4 — SQLite has no
/// network-style advisory lock, so the writer slot itself serves as the migration lock).
/// </summary>
/// <remarks>
/// <para>
/// After acquiring the writer slot the runner re-reads box-table state under the same
/// transaction and dispatches into one of three paths (fresh / bootstrap / normal) per
/// ADR 0057 §3 — the caller's <see cref="BoxTableState"/> is treated as a stale hint that
/// might have been invalidated between detection and lock-acquire (TOCTOU).
/// </para>
/// <para>
/// SQLite's grammar lacks <c>ALTER TABLE ADD COLUMN IF NOT EXISTS</c>, so each V2..V_latest
/// migration carries an <see cref="IAmABoxMigration.IdempotencyCheckSql"/> probing
/// <c>pragma_table_info</c>. The runner evaluates this scalar before applying
/// <see cref="IAmABoxMigration.UpScript"/> and skips the ALTER (still inserting the history row)
/// when the column is already present — this is the V1-already-ships-V_latest-shape case
/// described in the spec-0023-era transition plus the discriminator-shape bootstrap on legacy
/// tables.
/// </para>
/// <para>
/// The whole bootstrap/normal chain runs inside the single BEGIN IMMEDIATE transaction (per
/// ADR 0057 §5a — SQLite is whole-chain transactional like MSSQL/Postgres, unlike MySQL's
/// per-DDL implicit commit). A mid-chain failure rolls everything back, including the
/// history table itself if <c>EnsureHistoryTableAsync</c> ran inside the same transaction.
/// </para>
/// </remarks>
public class SqliteBoxMigrationRunner(
    IAmARelationalDatabaseConfiguration configuration,
    TimeSpan lockTimeout) : IAmABoxMigrationRunner
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";

    //SQLite returns SQLITE_BUSY (5) when a writer slot is contended. We retry on this code
    //only — any other SqliteException surfaces as a real failure.
    private const int SQLITE_BUSY = 5;

    //Backoff schedule: start at 25ms, double up to a 200ms cap, plus a small random jitter to
    //avoid lock-step retries between racing instances. Retries are bounded by lockTimeout.
    private static readonly TimeSpan s_initialBackoff = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan s_maxBackoff = TimeSpan.FromMilliseconds(200);

    private readonly TimeSpan _lockTimeout = lockTimeout;

    /// <summary>
    /// Convenience constructor using the default <see cref="BoxProvisioningOptions.MigrationLockTimeout"/>
    /// of 30 seconds. Tests and callers that need a custom timeout should use the two-argument form.
    /// </summary>
    public SqliteBoxMigrationRunner(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, TimeSpan.FromSeconds(30))
    {
    }

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = schemaName; // SQLite has no schema concept in this context.
        _ = tableState; // Stale hint — runner re-detects under the BEGIN IMMEDIATE transaction.

        using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await SetSqliteBusyTimeoutToZeroAsync(connection, cancellationToken);
        await EnsureWalModeAsync(connection, cancellationToken);

        var transaction = await BeginImmediateWithRetryAsync(connection, cancellationToken);
        try
        {
            await EnsureHistoryTableAsync(connection, transaction, cancellationToken);

            var tableExistsNow = await SqliteBoxDetectionHelpers.DoesTableExistAsync(
                connection, tableName, cancellationToken, transaction);
            var historyExistsNow = tableExistsNow && await SqliteBoxDetectionHelpers.DoesHistoryExistAsync(
                connection, tableName, cancellationToken, transaction);

            if (!tableExistsNow)
            {
                await RunFreshPathAsync(
                    connection, transaction, tableName, migrations, cancellationToken);
            }
            else if (!historyExistsNow)
            {
                await RunBootstrapPathAsync(
                    connection, transaction, tableName, boxType, migrations, cancellationToken);
            }
            else
            {
                await RunNormalPathAsync(
                    connection, transaction, tableName, migrations, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); } catch { /* connection may already be closed */ }
            throw;
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private static async Task RunFreshPathAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
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
        await ExecuteUpScriptAsync(connection, transaction, migrations[0].UpScript, cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, transaction, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    private static async Task RunBootstrapPathAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var detected = await SqliteBoxDetectionHelpers.DetectCurrentVersionAsync(
            connection, tableName, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = SqliteBoxDetectionHelpers.DiscriminatorFor(boxType);
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
            connection, transaction, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ApplyOrSkipAsync(connection, transaction, tableName, migration, cancellationToken);
        }
    }

    private static async Task RunNormalPathAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var maxVersion = await SqliteBoxDetectionHelpers.GetMaxVersionAsync(
            connection, tableName, cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            if (await IsMigrationAppliedAsync(
                    connection, transaction, tableName, migration.Version, cancellationToken))
                continue;

            await ApplyOrSkipAsync(connection, transaction, tableName, migration, cancellationToken);
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

    private async Task<SqliteTransaction> BeginImmediateWithRetryAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var backoff = s_initialBackoff;
        var random = new Random();

        while (true)
        {
            try
            {
                // Microsoft.Data.Sqlite's async BeginTransactionAsync overload doesn't expose the
                // `deferred` flag, so we use the synchronous form to guarantee BEGIN IMMEDIATE
                // (deferred: false). The call is local-only and fast — the long wait, if any, is
                // in Task.Delay below.
                return connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == SQLITE_BUSY)
            {
                if (stopwatch.Elapsed >= _lockTimeout)
                {
                    throw new TimeoutException(
                        $"Could not acquire SQLite writer lock within {_lockTimeout.TotalSeconds}s.",
                        ex);
                }

                var jitterMs = random.Next(0, (int)Math.Max(1, backoff.TotalMilliseconds / 4));
                var delay = backoff + TimeSpan.FromMilliseconds(jitterMs);
                if (stopwatch.Elapsed + delay > _lockTimeout)
                {
                    delay = _lockTimeout - stopwatch.Elapsed;
                    if (delay <= TimeSpan.Zero) delay = TimeSpan.FromMilliseconds(1);
                }

                await Task.Delay(delay, cancellationToken);
                backoff = backoff.Add(backoff);
                if (backoff > s_maxBackoff) backoff = s_maxBackoff;
            }
        }
    }

    private static async Task SetSqliteBusyTimeoutToZeroAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Disable Microsoft.Data.Sqlite's automatic busy_timeout (default = CommandTimeout = 30s)
        // so SQLite returns SQLITE_BUSY immediately and our explicit BeginImmediateWithRetryAsync
        // loop is the sole path for handling contention. Without this, the auto-retry would
        // mask MigrationLockTimeout and our backoff strategy would never run.
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 0;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureWalModeAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureHistoryTableAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
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

    private static async Task<bool> IsMigrationAppliedAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        string tableName, int version, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
SELECT COUNT(1) FROM [{MIGRATION_HISTORY_TABLE}]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @Version";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Version", version);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
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
