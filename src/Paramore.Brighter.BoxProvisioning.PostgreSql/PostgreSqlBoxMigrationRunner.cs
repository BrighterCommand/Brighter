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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Runs box migrations against a PostgreSQL database. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Uses an injected
/// <see cref="IPostgreSqlAdvisoryLock"/> (default <see cref="PostgreSqlAdvisoryLock"/>) for
/// concurrency control via <see cref="PostgreSqlProvisioningUnitOfWork"/>; the dispatch into
/// fresh / bootstrap / normal paths happens in the base after re-detection under the UoW.
/// </summary>
public class PostgreSqlBoxMigrationRunner : SqlBoxMigrationRunner<NpgsqlConnection, NpgsqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    // The history table is global — one row per (SchemaName, BoxTableName, MigrationVersion)
    // tracking migrations across every box-table schema. It always lives in "public" regardless
    // of the connection's search_path or the configured box schema; without explicit
    // qualification an unqualified CREATE/SELECT/INSERT would land in whichever schema appears
    // first on search_path, scattering history rows across the cluster.
    private const string HISTORY_TABLE_SCHEMA = "public";

    private readonly IPostgreSqlAdvisoryLock _advisoryLock;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public PostgreSqlBoxMigrationRunner(
        PostgreSqlBoxDetectionHelper detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        IPostgreSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<PostgreSqlBoxMigrationRunner>(),
            tracer, scope)
    {
        _advisoryLock = advisoryLock ?? new PostgreSqlAdvisoryLock();
    }

    /// <summary>
    /// Convenience ctor used by the <c>AddPostgreSqlOutbox</c>/<c>AddPostgreSqlInbox</c>
    /// registration extensions: synthesises a default <see cref="PostgreSqlBoxDetectionHelper"/>
    /// so the extension method doesn't have to resolve it from the container before the catalog
    /// is in scope.
    /// </summary>
    public PostgreSqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IPostgreSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : this(new PostgreSqlBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer, scope)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.Postgresql;

    /// <inheritdoc />
    protected override string? DefaultHistorySchema => HISTORY_TABLE_SCHEMA;

    /// <inheritdoc />
    protected override bool SupportsPerSchemaHistory => true;

    protected override async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<NpgsqlTransaction>> CreateUnitOfWorkAsync(
        NpgsqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<NpgsqlTransaction>>(
            new PostgreSqlProvisioningUnitOfWork(connection, _advisoryLock, Logger));

    // Include the schema in the lock key so two same-named tables in different schemas
    // (e.g. public.Outbox and billing.Outbox) acquire distinct advisory locks. Without
    // the schema qualifier they would share a lock and serialize unnecessarily — matches
    // the MSSQL runner's lockResource shape. Lowercase the identifiers so callers that
    // configure the same physical table with different casings (e.g. "Outbox" vs
    // "outbox" — both folded to `outbox` by PG) hash to the same lock key.
    protected override string LockResourceFor(string? schemaName, string tableName)
        => $"BrighterMigration_{PgIdentifier.Normalize(schemaName ?? HISTORY_TABLE_SCHEMA)}.{PgIdentifier.Normalize(tableName)}";


    /// <summary>
    /// Resolves and quotes the schema that physically holds the history table for this run, folded
    /// via <see cref="PgIdentifier.Quote"/> so it matches the case PostgreSQL stores. Under
    /// <see cref="MigrationHistoryScope.Global"/> this is <c>"public"</c> (today's behaviour); under
    /// <see cref="MigrationHistoryScope.PerSchema"/> it is the configured (non-null) SchemaName. The
    /// detection helper folds the same value the same way, so write and read never diverge.
    /// </summary>
    private string QuotedHistorySchema()
    {
        var schema = ResolveHistorySchema() ?? HISTORY_TABLE_SCHEMA;
        Identifiers.AssertSafe(schema, nameof(schema));
        return PgIdentifier.Quote(schema);
    }

    protected override async Task EnsureHistoryTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        // schemaName is accepted for symmetry with the abstract signature but ignored here — the
        // physical history schema comes from QuotedHistorySchema() (Global → "public"; PerSchema →
        // the configured SchemaName), the single source of truth shared with the INSERT path and
        // the detection helper. Explicit discard suppresses unused-parameter IDE warnings.
        // tableName is the box-table name; it will be consumed by the T9 D5 seed (PG flip
        // Global→PerSchema). Until then it is discarded for the same reason.
        _ = schemaName;
        _ = tableName;

        var historySchema = QuotedHistorySchema();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS {historySchema}.""{MIGRATION_HISTORY_TABLE}"" (
    ""MigrationVersion"" INT NOT NULL,
    ""SchemaName"" VARCHAR(256) NOT NULL DEFAULT 'public',
    ""BoxTableName"" VARCHAR(256) NOT NULL,
    ""Description"" VARCHAR(512) NOT NULL,
    ""AppliedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (""SchemaName"", ""BoxTableName"", ""MigrationVersion"")
)";
        // Postgres marks the outer transaction as aborted on a catalog-level duplicate-type
        // error from CREATE TABLE IF NOT EXISTS regardless of whether we catch it. Without a
        // savepoint the catch below leaves the transaction "poisoned" — RedetectStateAsync's
        // next statement then fails with 25P02 even though our swallow was intentional. A
        // SAVEPOINT around the CREATE narrows the abort to the inner sub-transaction, so
        // ROLLBACK TO SAVEPOINT restores a usable transaction state on the racing path.
        if (transaction is not null)
        {
            await transaction.SaveAsync(HistoryTableSavepoint, cancellationToken);
        }
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.ReleaseAsync(HistoryTableSavepoint, cancellationToken);
            }
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
            if (transaction is not null)
            {
                // Restore the transaction from the aborted sub-transaction back to the pre-CREATE
                // state. Subsequent statements (RedetectStateAsync etc.) then execute against a
                // live transaction instead of failing with 25P02.
                await transaction.RollbackAsync(HistoryTableSavepoint, cancellationToken);
            }

            // Surface the swallow so operators investigating "did two replicas race here?" have
            // a signal — Debug-level log (carrying the actual SqlState so the three race shapes
            // can be distinguished after the fact) + Activity event on the migration span (when
            // one is active). Silent swallow was the prior behaviour; per PR #4039 review item #7
            // the race is real and the swallow is intentional, but operators got no signal that
            // two racers serialised here.
            Logger.LogDebug(ex,
                "{HistoryTable} already created by racing session (Postgres SqlState {SqlState})",
                MIGRATION_HISTORY_TABLE, ex.SqlState);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
    }

    private const string HistoryTableSavepoint = "ensure_history_table";

    protected override async Task RunFreshPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        // Execute the V_latest-shape DDL sourced from IAmABoxMigrationCatalog.FreshInstallDdl
        // (the live builder DDL — typically <Backend>OutboxBuilder.GetDDL(...) for outbox /
        // <Backend>InboxBuilder.GetDDL(...) for inbox). We stamp directly at V_latest with a
        // "fresh install" marker — the V2..V_latest ALTERs in the chain would be no-ops on the
        // V_latest-shape table, so we skip the chain entirely (spec 0027 R1 fresh-install fast
        // path per ADR §3).
        await ExecuteDdlAsync(connection, transaction!, freshInstallDdl, cancellationToken);

        await InsertHistoryRowAsync(
            connection, transaction!, effectiveSchema, tableName,
            latestVersion, $"fresh install at V{latestVersion}", cancellationToken);
    }

    protected override async Task RunBootstrapPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        var detected = await DetectionHelper.DetectCurrentVersionAsync(
            connection, tableName, effectiveSchema, boxType, migrations, cancellationToken, transaction);

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
            connection, transaction!, effectiveSchema, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    protected override async Task RunNormalPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        var maxVersion = await DetectionHelper.GetMaxVersionAsync(
            connection, tableName, effectiveSchema, ResolveHistorySchema(), cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, transaction, migration.UpScript, cancellationToken);

    private static async Task ExecuteDdlAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string ddl, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertHistoryRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO {QuotedHistorySchema()}.""{MIGRATION_HISTORY_TABLE}"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        // Store PG-folded (lowercase) identifiers so history rows match the case of the
        // physical table PG actually created — and so lookups via the detection helper
        // (which normalizes the same way) hit the same row regardless of the configured
        // casing. Existing rows written before this normalization remain in the table but
        // become unreachable on read; the ALTER chain is idempotent (`ADD COLUMN IF NOT
        // EXISTS`) so a re-run against an existing table is a no-op.
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(schemaName));
        command.Parameters.AddWithValue("@BoxTableName", PgIdentifier.Normalize(tableName));
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
