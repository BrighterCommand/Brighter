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
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Runs box migrations against a MySQL database. Derives from
/// <see cref="RelationalBoxMigrationRunnerBase{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Uses an injected
/// <see cref="IMySqlAdvisoryLock"/> (default <see cref="MySqlAdvisoryLock"/>) for
/// session-scoped concurrency control via <see cref="MySqlProvisioningUnitOfWork"/>; the
/// dispatch into fresh / bootstrap / normal paths happens in the base after re-detection
/// under the UoW.
/// </summary>
/// <remarks>
/// Unlike MSSQL/Postgres, MySQL DDL has implicit per-statement commit (ADR 0057 §5a) so the
/// migration chain does NOT run inside a single transaction — each ALTER + history INSERT
/// commits independently. Recovery from mid-chain failure is therefore "resume from MAX(V) on
/// next invocation" rather than whole-chain rollback. The history table's PK on
/// <c>(SchemaName, BoxTableName, MigrationVersion)</c> ensures concurrent racers cannot double-stamp.
/// </remarks>
public class MySqlBoxMigrationRunner : RelationalBoxMigrationRunnerBase<MySqlConnection, MySqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";

    private readonly TimeSpan _lockTimeout;
    private readonly IMySqlAdvisoryLock _advisoryLock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public MySqlBoxMigrationRunner(
        MySqlBoxDetectionHelper detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        IMySqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null)
        : base(detectionHelper, configuration, lockTimeout ?? default, logger)
    {
        _lockTimeout = lockTimeout ?? default;
        _advisoryLock = advisoryLock ?? new MySqlAdvisoryLock();
        _logger = logger ?? ApplicationLogging.CreateLogger<MySqlBoxMigrationRunner>();
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises a default
    /// <see cref="MySqlBoxDetectionHelper"/>; removed when DI cascade lands in Phase 9.
    /// </summary>
    public MySqlBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMySqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null)
        : this(new MySqlBoxDetectionHelper(), configuration, advisoryLock, logger, lockTimeout)
    {
    }

    // ==== Hook overrides — Phase 7.3a delegates to legacy helpers ====

    protected override Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => OpenConnectionLegacyAsync(cancellationToken);

    protected override Task<IAmAProvisioningUnitOfWork<MySqlTransaction>> CreateUnitOfWorkAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
        => CreateUnitOfWorkLegacyAsync(connection, cancellationToken);

    protected override string LockResourceFor(string? schemaName, string tableName)
        => LockResourceForLegacy(schemaName, tableName);

    protected override Task EnsureHistoryTableAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken)
        => EnsureHistoryTableLegacyAsync(connection, cancellationToken);

    protected override Task RunFreshPathAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => RunFreshPathLegacyAsync(
            connection, schemaName ?? DatabaseName(), tableName, migrations, cancellationToken);

    protected override Task RunBootstrapPathAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => RunBootstrapPathLegacyAsync(
            connection, schemaName ?? DatabaseName(), tableName, boxType, migrations, cancellationToken);

    protected override Task RunNormalPathAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => RunNormalPathLegacyAsync(
            connection, schemaName ?? DatabaseName(), tableName, migrations, cancellationToken);

    // ==== Legacy delegates — Phase 7.3b moves bodies into overrides; Phase 7.3c deletes MigrateLegacyAsync ====

    private async Task<MySqlConnection> OpenConnectionLegacyAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(EnsureAllowUserVariables(Configuration.ConnectionString));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private Task<IAmAProvisioningUnitOfWork<MySqlTransaction>> CreateUnitOfWorkLegacyAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<MySqlTransaction>>(
            new MySqlProvisioningUnitOfWork(connection, _advisoryLock, _logger));

    // The schema is folded into the lock name so two same-named tables in different schemas
    // acquire distinct advisory locks. MySQL's GET_LOCK has a 64-char limit; the helper
    // hashes names exceeding it (long form) and otherwise preserves the simple form for
    // diagnostic readability. See MySqlMigrationLockName.
    private string LockResourceForLegacy(string? schemaName, string tableName)
        => MySqlMigrationLockName.For(schemaName ?? DatabaseName(), tableName);

    private async Task MigrateLegacyAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = tableState; // Stale hint — runner re-detects under the GET_LOCK session.
        var effectiveSchema = schemaName ?? DatabaseName();

        // Reject duplicate / gap / out-of-order versions before opening a connection. Validation
        // sits at MigrateAsync entry (rather than inside one of the path branches) so the rule
        // applies uniformly across fresh / bootstrap / normal paths — a malformed list corrupts
        // any of them (PK violation on history insert, skipped ALTERs, double-applied DDL).
        ValidateMigrationsMonotonic(effectiveSchema, tableName, migrations);

        var lockKey = MySqlMigrationLockName.For(effectiveSchema, tableName);

        using var connection = new MySqlConnection(EnsureAllowUserVariables(Configuration.ConnectionString));
        await connection.OpenAsync(cancellationToken);

        await _advisoryLock.AcquireAsync(connection, lockKey, _lockTimeout, cancellationToken);

        try
        {
            await EnsureHistoryTableLegacyAsync(connection, cancellationToken);

            var tableExistsNow = await MySqlBoxDetectionHelpers.DoesTableExistAsync(
                connection, tableName, effectiveSchema, cancellationToken);
            var historyExistsNow = tableExistsNow && await MySqlBoxDetectionHelpers.DoesHistoryExistAsync(
                connection, tableName, effectiveSchema, cancellationToken);

            if (!tableExistsNow)
            {
                await RunFreshPathLegacyAsync(
                    connection, effectiveSchema, tableName, migrations, cancellationToken);
            }
            else if (!historyExistsNow)
            {
                await RunBootstrapPathLegacyAsync(
                    connection, effectiveSchema, tableName, boxType, migrations, cancellationToken);
            }
            else
            {
                await RunNormalPathLegacyAsync(
                    connection, effectiveSchema, tableName, migrations, cancellationToken);
            }
        }
        finally
        {
            var releaseResult = await _advisoryLock.ReleaseAsync(connection, lockKey, cancellationToken);
            if (releaseResult is not true)
            {
                var resultMarker = releaseResult is null ? "NULL" : "0";
                _logger.LogWarning(
                    "MySQL advisory lock for migration of '{TableName}' (key '{LockKey}') was not released by this session: RELEASE_LOCK returned {Result} ({ResultMarker} = {ResultMeaning}). This is likely a Brighter defect — please report it.",
                    tableName, lockKey, releaseResult, resultMarker,
                    releaseResult is null ? "lock did not exist" : "lock held by another session");
            }
        }
    }

    private static async Task RunFreshPathLegacyAsync(
        MySqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        if (migrations.Count == 0) return;

        // V1's UpScript IS the live builder DDL (V_latest-shape per ADR §3 fresh-install fast
        // path). A list whose first entry is anything other than V1 would silently install the
        // wrong schema, so reject it before any DDL fires.
        if (migrations[0].Version != 1)
            throw new ConfigurationException(
                $"Cannot install '{schemaName}.{tableName}' from a fresh state: " +
                $"the first migration must be V1, but the supplied migrations list starts at V{migrations[0].Version}.");

        // We stamp directly at V_latest with a "fresh install" marker — V2..V_latest ALTERs
        // would be no-ops on the V_latest-shape table, so we skip them.
        await ExecuteUpScriptAsync(connection, migrations[0], cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, schemaName, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    private static async Task RunBootstrapPathLegacyAsync(
        MySqlConnection connection, string schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var detected = await MySqlBoxDetectionHelpers.DetectCurrentVersionAsync(
            connection, tableName, schemaName, boxType, migrations, cancellationToken);

        if (detected == -1)
        {
            var discriminator = MySqlBoxDetectionHelpers.DiscriminatorFor(boxType);
            throw new ConfigurationException(
                $"Table '{schemaName}.{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (detected == 0)
        {
            throw new ConfigurationException(
                $"Table '{schemaName}.{tableName}' does not match any known schema version. " +
                $"Cannot bootstrap a Brighter {boxType.ToString().ToLowerInvariant()} from an unrecognised column set.");
        }

        await InsertHistoryRowAsync(
            connection, schemaName, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static async Task RunNormalPathLegacyAsync(
        MySqlConnection connection, string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var maxVersion = await MySqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, tableName, schemaName, cancellationToken);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static async Task ExecuteUpScriptAsync(
        MySqlConnection connection, IAmABoxMigration migration,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = migration.UpScript;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureHistoryTableLegacyAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
    {
        // No race-handling needed: MySQL acquires a metadata lock (MDL_EXCLUSIVE) on the table
        // name for the duration of CREATE TABLE, and the IF NOT EXISTS check is evaluated under
        // that lock. Concurrent CREATE TABLE IF NOT EXISTS statements serialize via the MDL —
        // the loser sees the table already exists and emits a warning rather than an error.
        // (Contrast with Postgres, where the pg_class check and pg_type insert are not atomic.)
        using var command = connection.CreateCommand();
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS `{MIGRATION_HISTORY_TABLE}` (
    `MigrationVersion` INT NOT NULL,
    `SchemaName` VARCHAR(256) NOT NULL,
    `BoxTableName` VARCHAR(256) NOT NULL,
    `Description` VARCHAR(512) NOT NULL,
    `AppliedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SchemaName`, `BoxTableName`, `MigrationVersion`)
) ENGINE = InnoDB";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryRowAsync(
        MySqlConnection connection, string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
INSERT INTO `{MIGRATION_HISTORY_TABLE}` (`MigrationVersion`, `SchemaName`, `BoxTableName`, `Description`)
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(Configuration.ConnectionString);
        return builder.Database;
    }

    /// <summary>
    /// Ensures the connection string sets <c>AllowUserVariables=true</c>. The V2..V7 outbox/inbox
    /// migrations (per ADR 0057 §5) use the MySQL <c>information_schema.columns</c> +
    /// prepared-statement idempotency pattern, which depends on session-scoped user variables
    /// (<c>SET @q = ...; PREPARE stmt FROM @q;</c>). MySqlConnector treats <c>@variable</c> tokens
    /// as parameter markers by default and rejects them; this flag flips that behaviour so the
    /// prepared-statement form executes correctly. Transparent to caller-supplied connection
    /// strings — adds the flag if missing, preserves it if already set.
    /// </summary>
    private static string EnsureAllowUserVariables(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }
}
