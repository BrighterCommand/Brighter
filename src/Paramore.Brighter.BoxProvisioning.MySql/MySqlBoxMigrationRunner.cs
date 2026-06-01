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
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<MySqlBoxMigrationRunner>(),
            tracer, scope)
    {
        _advisoryLock = advisoryLock ?? new MySqlAdvisoryLock();
    }

    /// <summary>
    /// Convenience ctor used by the <c>AddMySqlOutbox</c>/<c>AddMySqlInbox</c> registration
    /// extensions: synthesises a default <see cref="MySqlBoxDetectionHelper"/> so the
    /// extension method doesn't have to resolve it from the container before the catalog
    /// is in scope.
    /// </summary>
    public MySqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMySqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : this(new MySqlBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer, scope)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.MySql;

    /// <inheritdoc />
    /// <remarks>MySQL's history table lives in the connection-bound database, not a static schema,
    /// so there is no constant default and <see cref="MigrationHistoryScope.PerSchema"/> is a no-op.</remarks>
    protected override string? DefaultHistorySchema => null;

    // ==== Hook overrides — Phase 7.3a delegates to legacy helpers ====

    protected override async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(EnsureAllowUserVariables(Configuration.ConnectionString));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<MySqlTransaction>> CreateUnitOfWorkAsync(
        MySqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        _ = schemaName;
        // The raw tableName threads into the UoW ctor for NF1-faithful tri-state RELEASE_LOCK
        // Warning emission — MySqlMigrationLockName.For hash-truncates long composites for the
        // GET_LOCK 64-char limit, so the lockResource string alone cannot surface the original
        // name. The base runner passes it explicitly per the LockResourceFor → CreateUnitOfWorkAsync
        // sequence; no cross-hook state is needed.
        return Task.FromResult<IAmAProvisioningUnitOfWork<MySqlTransaction>>(
            new MySqlProvisioningUnitOfWork(connection, _advisoryLock, Logger, tableName));
    }

    protected override string LockResourceFor(string? schemaName, string tableName)
    {
        // The schema is folded into the lock name so two same-named tables in different schemas
        // acquire distinct advisory locks. MySQL's GET_LOCK has a 64-char limit; the helper
        // hashes names exceeding it (long form) and otherwise preserves the simple form for
        // diagnostic readability. See MySqlMigrationLockName.
        return MySqlMigrationLockName.For(schemaName ?? DatabaseName(), tableName);
    }

    protected override async Task EnsureHistoryTableAsync(
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        // tableName is accepted for symmetry with the abstract signature; MySQL is out of scope
        // for PerSchema history placement (SupportsPerSchemaHistory => false) so the D5 seed never
        // fires here. Discard to suppress unused-parameter warnings.
        _ = tableName;
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
        // V_latest-shape table, so we skip the chain entirely 
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
            connection, tableName, effectiveSchema, ResolveHistorySchema(), cancellationToken);

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
    /// migrations use the MySQL <c>information_schema.columns</c> +  prepared-statement idempotency
    /// pattern, which depends on session-scoped user variables  (<c>SET @q = ...; PREPARE stmt FROM @q;</c>).
    /// MySqlConnector treats <c>@variable</c> tokens as parameter markers by default and rejects them;
    /// this flag flips that behaviour so the  prepared-statement form executes correctly. Transparent to
    /// caller-supplied connection  strings — adds the flag if missing, preserves it if already set. When the flag is
    /// flipped, an Information log records the mutation so a security-conscious operator who
    /// deliberately disabled user variables (user-variable abuse via prepared-statement
    /// injection is a documented threat surface) can correlate it against migration activity
    /// without needing to enable Debug-level logging. 
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
