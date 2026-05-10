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
using Npgsql;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Runs box migrations against a PostgreSQL database. Derives from
/// <see cref="RelationalBoxMigrationRunnerBase{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Uses an injected
/// <see cref="IPostgreSqlAdvisoryLock"/> (default <see cref="PostgreSqlAdvisoryLock"/>) for
/// concurrency control via <see cref="PostgreSqlProvisioningUnitOfWork"/>; the dispatch into
/// fresh / bootstrap / normal paths happens in the base after re-detection under the UoW.
/// </summary>
public class PostgreSqlBoxMigrationRunner : RelationalBoxMigrationRunnerBase<NpgsqlConnection, NpgsqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    // The history table is global — one row per (SchemaName, BoxTableName, MigrationVersion)
    // tracking migrations across every box-table schema. It always lives in "public" regardless
    // of the connection's search_path or the configured box schema; without explicit
    // qualification an unqualified CREATE/SELECT/INSERT would land in whichever schema appears
    // first on search_path, scattering history rows across the cluster.
    private const string HISTORY_TABLE_SCHEMA = "public";

    private readonly TimeSpan _lockTimeout;
    private readonly IPostgreSqlAdvisoryLock _advisoryLock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public PostgreSqlBoxMigrationRunner(
        PostgreSqlBoxDetectionHelper detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        IPostgreSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null)
        : base(detectionHelper, configuration, lockTimeout ?? default, logger)
    {
        _lockTimeout = lockTimeout ?? default;
        _advisoryLock = advisoryLock ?? new PostgreSqlAdvisoryLock();
        _logger = logger ?? ApplicationLogging.CreateLogger<PostgreSqlBoxMigrationRunner>();
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises a default
    /// <see cref="PostgreSqlBoxDetectionHelper"/>; removed when DI cascade lands in Phase 9.
    /// </summary>
    public PostgreSqlBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IPostgreSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null)
        : this(new PostgreSqlBoxDetectionHelper(), configuration, advisoryLock, logger, lockTimeout)
    {
    }

    // ==== Hook overrides — Phase 7.2a delegates to legacy helpers ====

    protected override async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<NpgsqlTransaction>> CreateUnitOfWorkAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<NpgsqlTransaction>>(
            new PostgreSqlProvisioningUnitOfWork(connection, _advisoryLock, _logger));

    // Include the schema in the lock key so two same-named tables in different schemas
    // (e.g. public.Outbox and billing.Outbox) acquire distinct advisory locks. Without
    // the schema qualifier they would share a lock and serialize unnecessarily — matches
    // the MSSQL runner's lockResource shape.
    protected override string LockResourceFor(string? schemaName, string tableName)
        => $"BrighterMigration_{schemaName ?? HISTORY_TABLE_SCHEMA}.{tableName}";

    protected override async Task EnsureHistoryTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken)
    {
        // schemaName is accepted for symmetry with the abstract signature but ignored — the
        // history table always lives in [public] regardless of the configured box schema (see
        // HISTORY_TABLE_SCHEMA constant comment).
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS ""{HISTORY_TABLE_SCHEMA}"".""{MIGRATION_HISTORY_TABLE}"" (
    ""MigrationVersion"" INT NOT NULL,
    ""SchemaName"" VARCHAR(256) NOT NULL DEFAULT 'public',
    ""BoxTableName"" VARCHAR(256) NOT NULL,
    ""Description"" VARCHAR(512) NOT NULL,
    ""AppliedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (""SchemaName"", ""BoxTableName"", ""MigrationVersion"")
)";
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation
            || ex.SqlState == PostgresErrorCodes.DuplicateTable
            || ex.SqlState == PostgresErrorCodes.DuplicateObject)
        {
            // TOCTOU on Postgres catalog: another connection raced our CREATE TABLE IF NOT EXISTS
            // between the existence check (pg_class) and the type insert (pg_type), or between
            // the type insert and a duplicate-relation check. The error surfaces as one of:
            //   23505 (UniqueViolation) on pg_type_typname_nsp_index — most common
            //   42P07 (DuplicateTable) — relation already exists
            //   42710 (DuplicateObject) — type/constraint already exists
            // In every case the history table now exists with the schema we intended to create
            // (the racing session ran the same DDL), which is the post-condition we wanted.
        }
    }

    protected override Task RunFreshPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => RunFreshPathLegacyAsync(
            connection, transaction!, schemaName ?? HISTORY_TABLE_SCHEMA, tableName, migrations, cancellationToken);

    protected override Task RunBootstrapPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => RunBootstrapPathLegacyAsync(
            connection, transaction!, schemaName ?? HISTORY_TABLE_SCHEMA, tableName, boxType, migrations, cancellationToken);

    protected override Task RunNormalPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => RunNormalPathLegacyAsync(
            connection, transaction!, schemaName ?? HISTORY_TABLE_SCHEMA, tableName, migrations, cancellationToken);

    // ==== Legacy delegates — Phase 7.2b moves bodies into overrides; Phase 7.2c deletes MigrateLegacyAsync ====

    private async Task MigrateLegacyAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        _ = tableState; // Stale hint — runner re-detects under the advisory lock.
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        // Reject duplicate / gap / out-of-order versions before opening a connection. Validation
        // sits at MigrateAsync entry (rather than inside one of the path branches) so the rule
        // applies uniformly across fresh / bootstrap / normal paths — a malformed list corrupts
        // any of them (PK violation on history insert, skipped ALTERs, double-applied DDL).
        ValidateMigrationsMonotonic(effectiveSchema, tableName, migrations);

        // Include the schema in the lock key so two same-named tables in different schemas
        // (e.g. public.Outbox and billing.Outbox) acquire distinct advisory locks. Without
        // the schema qualifier they would share a lock and serialize unnecessarily — matches
        // the MSSQL runner's lockResource shape at MsSqlBoxMigrationRunner.cs:90.
        var lockKey = $"BrighterMigration_{effectiveSchema}.{tableName}";

        using var connection = new NpgsqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await _advisoryLock.AcquireAsync(connection, lockKey, _lockTimeout, cancellationToken);

        try
        {
            // Run the history-table create OUTSIDE the migration transaction. CREATE TABLE IF
            // NOT EXISTS is not atomic in the Postgres catalog — the existence check on pg_class
            // and the type insert into pg_type are separate steps, so two concurrent provisioners
            // (the per-table advisory lock above does not cover the shared history table) can
            // both pass the existence check and both try to add the type, with one losing on
            // pg_type_typname_nsp_index (23505). Inside a transaction that failure poisons the
            // session (every subsequent statement returns 25P02); pulling it out to autocommit
            // means only the racing statement fails and we can ignore it.
            await EnsureHistoryTableAsync(connection, transaction: null, schemaName: null, cancellationToken);

            var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var tableExistsNow = await PostgreSqlBoxDetectionHelpers.DoesTableExistAsync(
                    connection, tableName, effectiveSchema, cancellationToken, transaction);
                var historyExistsNow = tableExistsNow && await PostgreSqlBoxDetectionHelpers.DoesHistoryExistAsync(
                    connection, tableName, effectiveSchema, cancellationToken, transaction);

                if (!tableExistsNow)
                {
                    await RunFreshPathLegacyAsync(
                        connection, transaction, effectiveSchema, tableName, migrations, cancellationToken);
                }
                else if (!historyExistsNow)
                {
                    await RunBootstrapPathLegacyAsync(
                        connection, transaction, effectiveSchema, tableName, boxType, migrations, cancellationToken);
                }
                else
                {
                    await RunNormalPathLegacyAsync(
                        connection, transaction, effectiveSchema, tableName, migrations, cancellationToken);
                }

                transaction.Commit();
            }
            catch
            {
                try { transaction.Rollback(); } catch { /* connection may already be closed */ }
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }
        finally
        {
            var held = await _advisoryLock.ReleaseAsync(connection, lockKey, cancellationToken);
            if (!held)
            {
                _logger.LogWarning(
                    "Postgres advisory lock for migration of '{TableName}' (key '{LockKey}') was not held by this session at release; pg_advisory_unlock returned false. This is likely a Brighter defect — please report it.",
                    tableName, lockKey);
            }
        }
    }

    private async Task RunFreshPathLegacyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
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
        await ExecuteUpScriptAsync(connection, transaction, migrations[0], cancellationToken);

        var latest = migrations[migrations.Count - 1];
        await InsertHistoryRowAsync(
            connection, transaction, schemaName, tableName,
            latest.Version, $"fresh install at V{latest.Version}", cancellationToken);
    }

    private async Task RunBootstrapPathLegacyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var detected = await PostgreSqlBoxDetectionHelpers.DetectCurrentVersionAsync(
            connection, tableName, schemaName, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = PostgreSqlBoxDetectionHelpers.DiscriminatorFor(boxType);
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
            connection, transaction, schemaName, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, transaction, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private async Task RunNormalPathLegacyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var maxVersion = await PostgreSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, tableName, schemaName, cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, transaction, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction, schemaName, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }


    private static async Task ExecuteUpScriptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = migration.UpScript;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO ""{HISTORY_TABLE_SCHEMA}"".""{MIGRATION_HISTORY_TABLE}"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
