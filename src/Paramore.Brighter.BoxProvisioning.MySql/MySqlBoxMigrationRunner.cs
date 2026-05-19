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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Runs box migrations against a MySQL database. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for the
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
public class MySqlBoxMigrationRunner : SqlBoxMigrationRunner<MySqlConnection, MySqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";

    private readonly IMySqlAdvisoryLock _advisoryLock;

    // Threaded through the base's LockResourceFor → CreateUnitOfWorkAsync sequence so the
    // per-invocation tableName reaches MySqlProvisioningUnitOfWork's ctor for NF1-faithful
    // tri-state RELEASE_LOCK Warning emission (MySqlMigrationLockName.For hash-truncates the
    // raw tableName for long composites, so lockResource alone cannot surface it). AsyncLocal
    // preserves correctness under concurrent MigrateAsync invocations on the same instance.
    private readonly AsyncLocal<string?> _activeTableName = new();

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public MySqlBoxMigrationRunner(
        MySqlBoxDetectionHelper detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        IMySqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        IAmABrighterTracer? tracer = null)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<MySqlBoxMigrationRunner>(),
            tracer)
    {
        _advisoryLock = advisoryLock ?? new MySqlAdvisoryLock();
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises a default
    /// <see cref="MySqlBoxDetectionHelper"/>; removed when DI cascade lands in Phase 9.
    /// </summary>
    public MySqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMySqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null)
        : this(new MySqlBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.MySql;

    // ==== Hook overrides — Phase 7.3a delegates to legacy helpers ====

    protected override async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(EnsureAllowUserVariables(Configuration.ConnectionString));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<MySqlTransaction>> CreateUnitOfWorkAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
    {
        // Capture _activeTableName.Value into the UoW ctor and immediately clear the
        // AsyncLocal so the value does not leak onto the ExecutionContext of any continuation
        // observed after MigrateAsync returns — per PR #4039 reviewer item M4-4. The base
        // runner's hook sequence guarantees LockResourceFor (which sets the value) runs
        // exactly once before CreateUnitOfWorkAsync (which reads it), so clearing here is
        // safe: no other hook reads _activeTableName, and the UoW has already captured the
        // string into its `_tableName` field by the time the AsyncLocal is reset.
        try
        {
            return Task.FromResult<IAmAProvisioningUnitOfWork<MySqlTransaction>>(
                new MySqlProvisioningUnitOfWork(connection, _advisoryLock, Logger, _activeTableName.Value));
        }
        finally
        {
            _activeTableName.Value = null;
        }
    }

    protected override string LockResourceFor(string? schemaName, string tableName)
    {
        // Capture the per-invocation table name so CreateUnitOfWorkAsync can thread it into
        // the UoW ctor for NF1-faithful tri-state Warning emission — MySqlMigrationLockName.For
        // hash-truncates long composites and would otherwise lose the raw tableName.
        _activeTableName.Value = tableName;
        // The schema is folded into the lock name so two same-named tables in different schemas
        // acquire distinct advisory locks. MySQL's GET_LOCK has a 64-char limit; the helper
        // hashes names exceeding it (long form) and otherwise preserves the simple form for
        // diagnostic readability. See MySqlMigrationLockName.
        return MySqlMigrationLockName.For(schemaName ?? DatabaseName(), tableName);
    }

    protected override async Task EnsureHistoryTableAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken)
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

    protected override async Task RunFreshPathAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? DatabaseName();

        // Execute the V_latest-shape DDL sourced from IAmABoxMigrationCatalog.FreshInstallDdl
        // (the live builder DDL — typically <Backend>OutboxBuilder.GetDDL(...) for outbox /
        // <Backend>InboxBuilder.GetDDL(...) for inbox). We stamp directly at V_latest with a
        // "fresh install" marker — the V2..V_latest ALTERs in the chain would be no-ops on the
        // V_latest-shape table, so we skip the chain entirely (spec 0027 R1 fresh-install fast
        // path per ADR §3).
        await ExecuteDdlAsync(connection, freshInstallDdl, cancellationToken);

        await InsertHistoryRowAsync(
            connection, effectiveSchema, tableName,
            latestVersion, $"fresh install at V{latestVersion}", cancellationToken);
    }

    protected override async Task RunBootstrapPathAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? DatabaseName();

        var detected = await DetectionHelper.DetectCurrentVersionAsync(
            connection, tableName, effectiveSchema, boxType, migrations, cancellationToken);

        if (detected == -1)
        {
            var discriminator = DetectionHelper.DiscriminatorFor(boxType);
            throw new ConfigurationException(
                $"Table '{effectiveSchema}.{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (detected == 0)
        {
            throw new ConfigurationException(
                $"Table '{effectiveSchema}.{tableName}' does not match any known schema version. " +
                $"Cannot bootstrap a Brighter {boxType.ToString().ToLowerInvariant()} from an unrecognised column set.");
        }

        await InsertHistoryRowAsync(
            connection, effectiveSchema, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    protected override async Task RunNormalPathAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? DatabaseName();
        var maxVersion = await DetectionHelper.GetMaxVersionAsync(
            connection, tableName, effectiveSchema, cancellationToken);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        MySqlConnection connection, IAmABoxMigration migration,
        CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, migration.UpScript, cancellationToken);

    private static async Task ExecuteDdlAsync(
        MySqlConnection connection, string ddl,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = ddl;
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
    /// strings — adds the flag if missing, preserves it if already set. When the flag is
    /// flipped, an Information log records the mutation so a security-conscious operator who
    /// deliberately disabled user variables (user-variable abuse via prepared-statement
    /// injection is a documented threat surface) can correlate it against migration activity
    /// without needing to enable Debug-level logging. Per PR #4039 reviewer items M2-4 / M4-2.
    /// </summary>
    private string EnsureAllowUserVariables(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (builder.AllowUserVariables) return builder.ConnectionString;

        Logger.LogInformation(
            "MySQL box-migration runner: enabling AllowUserVariables=true on the migration connection (caller's connection string had it disabled). Required by the V2..V7 prepared-statement idempotency pattern per ADR 0057 §5a; the mutation is scoped to the runner's own MySqlConnection and does not affect the caller-supplied connection string instance.");
        builder.AllowUserVariables = true;
        return builder.ConnectionString;
    }
}
