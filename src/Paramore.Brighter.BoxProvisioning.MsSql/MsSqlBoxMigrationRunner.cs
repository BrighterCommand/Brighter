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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Runs box migrations against a MSSQL database. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Uses an injected
/// <see cref="IMsSqlAdvisoryLock"/> (default <see cref="MsSqlAdvisoryLock"/>) for
/// concurrency control via <see cref="MsSqlProvisioningUnitOfWork"/>; the dispatch into
/// fresh / bootstrap / normal paths happens in the base after re-detection under the UoW.
/// </summary>
public class MsSqlBoxMigrationRunner : SqlBoxMigrationRunner<SqlConnection, SqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    // The history table is global — one row per (SchemaName, BoxTableName, MigrationVersion)
    // tracking migrations across every box-table schema. It always lives in [dbo] regardless of
    // the connection's default schema or the configured box schema.
    private const string HISTORY_TABLE_SCHEMA = "dbo";

    // Lock-timeout validation lives inside MsSqlAdvisoryLock.AcquireAsync (per ADR 0057 §5b)
    // so any caller of the abstraction is protected. A bad timeout surfaces as
    // ArgumentOutOfRangeException on first MigrateAsync call rather than at construction.
    private readonly IMsSqlAdvisoryLock _advisoryLock;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public MsSqlBoxMigrationRunner(
        MsSqlBoxDetectionHelper detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        IMsSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<MsSqlBoxMigrationRunner>(),
            tracer, scope)
    {
        _advisoryLock = advisoryLock ?? new MsSqlAdvisoryLock();
    }

    /// <summary>
    /// Convenience ctor used by the <c>AddMsSqlOutbox</c>/<c>AddMsSqlInbox</c> registration
    /// extensions: synthesises a default <see cref="MsSqlBoxDetectionHelper"/> so the
    /// extension method doesn't have to resolve it from the container before the catalog
    /// is in scope.
    /// </summary>
    public MsSqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMsSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : this(new MsSqlBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer, scope)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.MsSql;

    /// <inheritdoc />
    protected override string? DefaultHistorySchema => HISTORY_TABLE_SCHEMA;

    /// <inheritdoc />
    protected override bool SupportsPerSchemaHistory => true;

    // ==== Hook overrides — Phase 7.1a delegates to legacy helpers ====

    protected override async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<SqlTransaction>>(
            new MsSqlProvisioningUnitOfWork(connection, _advisoryLock, Logger));

    // Include the schema name so that two same-named tables in different schemas
    // (e.g. dbo.Outbox and billing.Outbox) acquire distinct advisory locks. Without
    // the schema qualifier they would share a lock and serialize unnecessarily.
    protected override string LockResourceFor(string? schemaName, string tableName)
        => $"BrighterMigration_{schemaName ?? HISTORY_TABLE_SCHEMA}.{tableName}";

    protected override async Task EnsureHistoryTableAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        // Under PerSchema the history table is placed in the configured schema; under Global it is
        // the backend default (dbo). ResolveHistoryTableSchema() is the single source of truth
        // shared with the read side so they cannot diverge. AssertSafe + bracket-quote because
        // T-SQL DDL cannot parameterize the schema in CREATE TABLE; the @HistorySchema bind param
        // still drives the SCHEMA_ID(...) existence probe.
        var historySchema = ResolveHistoryTableSchema();

        // D5 (ADR 0060): on a first PerSchema run that follows a Global predecessor, copy this
        // tenant's prior history rows from the legacy default-schema table so the runner does NOT
        // mis-detect the box as needing a bootstrap re-stamp (which would silently re-run migration
        // accounting and break FR5). We probe the per-schema table's existence BEFORE the CREATE so
        // we can distinguish "just created — needs seed" from "already there — no seed needed".
        // The probe runs under the same advisory lock + transaction as the CREATE and seed below,
        // so a racing flip cannot interleave between them. Skipped when the resolved history schema
        // equals the backend default (Global, or PerSchema with SchemaName == dbo) — then per-schema
        // and legacy are the same table and there is nothing to copy.
        var needsSeedCheck = !string.Equals(historySchema, HISTORY_TABLE_SCHEMA, StringComparison.OrdinalIgnoreCase);
        var perSchemaExisted = needsSeedCheck
            && await DoesHistoryTableExistAsync(connection, transaction, historySchema, cancellationToken);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            // Filter sys.tables by both name AND schema_id — without the schema_id filter the
            // existence check misfires when any other schema happens to contain a table by that name,
            // skipping the create and breaking subsequent INSERT/SELECT statements.
            // The WHERE values are parameterised (matching DoesTableExistAsync); the bracketed
            // identifiers in the CREATE TABLE body must remain inline because T-SQL DDL does not
            // accept bind parameters for object names.
            // SET XACT_ABORT OFF is issued defensively so the 2714 swallow below works for callers
            // who pre-enable XACT_ABORT ON on their connection pool (some ORM defaults, startup
            // scripts). With XACT_ABORT ON, 2714 dooms the transaction rather than being statement-
            // terminating, and RedetectStateAsync would fail with state error 3930. We restore the
            // session default OFF for the runner's own connection only — scope is bounded to this
            // command. Per PR #4039 reviewer item F2-3.
            command.CommandText = $@"
SET XACT_ABORT OFF;
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name = @HistoryTableName AND schema_id = SCHEMA_ID(@HistorySchema)
)
BEGIN
    CREATE TABLE [{historySchema}].[{MIGRATION_HISTORY_TABLE}] (
        [MigrationVersion] INT NOT NULL,
        [SchemaName] VARCHAR(256) NOT NULL DEFAULT 'dbo',
        [BoxTableName] VARCHAR(256) NOT NULL,
        [Description] NVARCHAR(512) NOT NULL,
        [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_BrighterMigrationHistory
            PRIMARY KEY ([SchemaName], [BoxTableName], [MigrationVersion])
    );
END";
            command.Parameters.AddWithValue("@HistoryTableName", MIGRATION_HISTORY_TABLE);
            command.Parameters.AddWithValue("@HistorySchema", historySchema);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == 2714)
            {
                // TOCTOU on sys.tables: the per-table sp_getapplock above does not cover the shared
                // __BrighterMigrationHistory, so two concurrent provisioners with different table
                // names (e.g. outbox + inbox) can both pass the IF NOT EXISTS check and both issue
                // CREATE TABLE; the loser hits 2714 ("There is already an object named ..."). 2714
                // is a statement-terminating error with default XACT_ABORT OFF — the transaction is
                // not doomed, so we can ignore it and continue. The history table now exists with
                // the schema we intended (the racing session ran the same DDL).
                //
                // Surface the swallow so operators investigating "did two replicas race here?" have
                // a signal — Debug-level log + Activity event on the migration span (when one is
                // active). Silent swallow was the prior behaviour; per PR #4039 review item #7 the
                // race is real and the swallow is intentional, but operators got no signal that
                // two racers serialised here.
                Logger.LogDebug(ex,
                    "{HistoryTable} already created by racing session (MsSql 2714)",
                    MIGRATION_HISTORY_TABLE);
                Activity.Current?.AddEvent(new ActivityEvent(
                    BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
            }
        }

        // Run the D5 seed only when the per-schema table was absent before this run. If it was
        // already there, this PerSchema deployment is already past its first run and any seed must
        // have been done previously. The seed itself carries a NOT EXISTS guard against the
        // composite PK so a re-fire (e.g. if a 2714 racer beat us to the seed) is harmless.
        if (needsSeedCheck && !perSchemaExisted)
        {
            await SeedHistoryFromLegacyAsync(
                connection, transaction, historySchema, schemaName ?? historySchema, tableName,
                cancellationToken);
        }
    }

    // EXISTS probe used both for the D5 pre-create check on the per-schema table and the
    // legacy-existence check inside SeedHistoryFromLegacyAsync. Filtering by both name and
    // schema name (via the join to sys.schemas) ensures a same-named table in a third schema
    // does not register as a hit.
    private async Task<bool> DoesHistoryTableExistAsync(
        SqlConnection connection, SqlTransaction? transaction, string schema,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT CASE WHEN EXISTS(" +
            "SELECT 1 FROM sys.tables t " +
            "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE t.name = @HistoryTableName AND s.name = @SchemaName" +
            ") THEN 1 ELSE 0 END";
        command.Parameters.AddWithValue("@HistoryTableName", MIGRATION_HISTORY_TABLE);
        command.Parameters.AddWithValue("@SchemaName", schema);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && (int)result == 1;
    }

    // Copies this tenant's prior history rows from the legacy default-schema history table into the
    // newly-created per-schema history table on first PerSchema run (ADR 0060 D5). Filtered to this
    // tenant (SchemaName + BoxTableName) so a multi-tenant Global deployment doesn't bleed rows
    // across tenants. All five columns are copied — Description is NOT NULL with no default and
    // AppliedAt preserves the original install/bootstrap timestamp, so the post-flip row is
    // indistinguishable from a row written by the original Global-scope provision. NOT EXISTS on
    // the composite PK makes the seed idempotent. Skipped when no legacy table exists — this is
    // the first-ever provision (no Global predecessor) and there is nothing to copy.
    private async Task SeedHistoryFromLegacyAsync(
        SqlConnection connection, SqlTransaction? transaction,
        string perSchema, string boxSchema, string boxTableName,
        CancellationToken cancellationToken)
    {
        const string legacySchema = HISTORY_TABLE_SCHEMA;
        var legacyExists = await DoesHistoryTableExistAsync(connection, transaction, legacySchema, cancellationToken);
        if (!legacyExists)
        {
            return;
        }

        // The perSchema identifier was already AssertSafe-d via ResolveHistoryTableSchema. legacySchema
        // is the compile-time const HISTORY_TABLE_SCHEMA ("dbo") — trivially safe — so no AssertSafe
        // call is needed. If a future refactor turns legacySchema into a non-const derived value
        // (e.g. an operator-configurable legacy-schema override) restore the AssertSafe check. boxSchema
        // is operator-supplied and must be validated before inlining into the parameterised WHERE
        // clause's surrounding SQL.
        Identifiers.AssertSafe(boxSchema, nameof(boxSchema));

        int rowsCopied;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $@"
INSERT INTO [{perSchema}].[{MIGRATION_HISTORY_TABLE}]
    ([MigrationVersion], [SchemaName], [BoxTableName], [Description], [AppliedAt])
SELECT src.[MigrationVersion], src.[SchemaName], src.[BoxTableName], src.[Description], src.[AppliedAt]
FROM [{legacySchema}].[{MIGRATION_HISTORY_TABLE}] AS src
WHERE src.[SchemaName] = @SchemaName
  AND src.[BoxTableName] = @BoxTableName
  AND NOT EXISTS (
      SELECT 1 FROM [{perSchema}].[{MIGRATION_HISTORY_TABLE}] AS tgt
      WHERE tgt.[SchemaName] = src.[SchemaName]
        AND tgt.[BoxTableName] = src.[BoxTableName]
        AND tgt.[MigrationVersion] = src.[MigrationVersion])";
            command.Parameters.AddWithValue("@SchemaName", boxSchema);
            command.Parameters.AddWithValue("@BoxTableName", boxTableName);
            try
            {
                rowsCopied = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                // Reviewer #1 hardening (must, not should): any failure reading the legacy history
                // table — typically tenant-isolated credentials lacking SELECT on dbo — must surface
                // as a ConfigurationException with a clear cause. Silently absorbing the failure
                // would let an empty seed go through; the next provision would then bootstrap-stamp
                // a fresh row, effectively re-running the migration ledger and breaking FR5. The
                // throw rolls back the surrounding transaction so no partial per-schema state is
                // left behind.
                throw new ConfigurationException(
                    $"Brighter PerSchema migration: the first Global → PerSchema run requires read " +
                    $"access to the legacy default-schema history table [{legacySchema}].[{MIGRATION_HISTORY_TABLE}] " +
                    $"so prior history rows can be seeded into the per-schema history. Grant the runner " +
                    $"SELECT on that table (and INSERT on [{perSchema}].[{MIGRATION_HISTORY_TABLE}]) and retry. " +
                    $"The original provider exception is preserved as the inner exception.",
                    ex);
            }
        }

        if (rowsCopied > 0)
        {
            // Spec 0029 NF5/AC7 (ADR 0060 D6, reviewer #3): structured fields RowCount + BoxTable +
            // LegacySchema + TargetSchema each appear as a separate placeholder so a structured log
            // sink can filter on individual fields (e.g. RowCount>0, TargetSchema='billing_x'). The
            // existing legacy-history-seeded Activity event additionally carries the row count as a
            // tag so a trace-store query can size flip impact without parsing event names.
            Logger.LogInformation(
                "Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}",
                rowsCopied, boxTableName, legacySchema, perSchema);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventLegacyHistorySeeded,
                default,
                new ActivityTagsCollection
                {
                    { BrighterSemanticConventions.BoxMigrationSeedRowCount, rowsCopied }
                }));
        }
    }

    protected override async Task RunFreshPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
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
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
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
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
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
        SqlConnection connection, SqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, transaction, migration.UpScript, cancellationToken);

    private static async Task ExecuteDdlAsync(
        SqlConnection connection, SqlTransaction transaction,
        string ddl, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Resolves the physical schema that holds the history table for this run, validated for safe
    // inline interpolation into DDL/DML. PerSchema → configured schema; otherwise the backend
    // default (dbo). Shared by EnsureHistoryTableAsync (CREATE) and InsertHistoryRowAsync (INSERT)
    // so the write side never diverges from the read side's ResolveHistorySchema(). The `!` is
    // safe: DefaultHistorySchema returns the non-null "dbo" const on this runner and the D3 guard
    // rejects PerSchema with a null SchemaName before MigrateAsync proceeds, so ResolveHistorySchema()
    // is provably non-null here. If a future refactor breaks that invariant, NRE points at this line.
    private string ResolveHistoryTableSchema()
    {
        var historySchema = ResolveHistorySchema()!;
        Identifiers.AssertSafe(historySchema, nameof(historySchema));
        return historySchema;
    }

    private async Task InsertHistoryRowAsync(
        SqlConnection connection, SqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        var historySchema = ResolveHistoryTableSchema();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO [{historySchema}].[{MIGRATION_HISTORY_TABLE}] ([MigrationVersion], [SchemaName], [BoxTableName], [Description])
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
