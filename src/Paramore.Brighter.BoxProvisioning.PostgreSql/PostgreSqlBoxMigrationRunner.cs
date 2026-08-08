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
        // The `!` is safe: DefaultHistorySchema returns the non-null "public" const on this runner
        // and the D3 guard rejects PerSchema with a null SchemaName before MigrateAsync proceeds,
        // so ResolveHistorySchema() is provably non-null here. If a future refactor breaks that
        // invariant, NRE points at this line.
        var schema = ResolveHistorySchema()!;
        Identifiers.AssertSafe(schema, nameof(schema));
        return PgIdentifier.Quote(schema);
    }

    protected override async Task EnsureHistoryTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        // Resolve the physical history schema once. Under PerSchema it's the configured (non-null)
        // SchemaName; under Global it is the backend default ("public"). The detection helper folds
        // the same value the same way (PgIdentifier.Quote / Normalize), so write and read never
        // diverge. AssertSafe guards against any unsafe schema literal leaking into the inline DDL.
        // The `!` is safe: DefaultHistorySchema returns the non-null "public" const on this runner
        // and the D3 guard rejects PerSchema with a null SchemaName before MigrateAsync proceeds,
        // so ResolveHistorySchema() is provably non-null here.
        var resolvedHistorySchema = ResolveHistorySchema()!;
        Identifiers.AssertSafe(resolvedHistorySchema, nameof(resolvedHistorySchema));
        var historySchemaQuoted = PgIdentifier.Quote(resolvedHistorySchema);
        var historySchemaFolded = PgIdentifier.Normalize(resolvedHistorySchema);

        // D5 (ADR 0060): on a PerSchema run that follows a Global predecessor, copy this tenant's
        // prior history rows from the legacy public.__BrighterMigrationHistory so the runner does
        // NOT mis-detect the box as needing a bootstrap re-stamp (which would silently re-run
        // migration accounting and break FR5). Skipped when the folded resolved history schema
        // equals the folded backend default (Global, or PerSchema with SchemaName == "public") —
        // then per-schema and legacy are the same table and there is nothing to copy.
        //
        // PR #4155 reviewer bug fix: an earlier implementation gated the seed on whether the
        // per-schema __BrighterMigrationHistory existed before this run, so the second box-type to
        // flip (e.g. inbox after outbox) found the table already created by the first flip and
        // skipped the seed — its legacy row never landed and the bootstrap path stamped a fresh
        // "bootstrap: detected at V_latest" entry. The table-level gate has been removed: the
        // seed's per-row NOT EXISTS PK guard already makes it idempotent (a fresh PerSchema
        // install with no legacy table is a no-op because the seed's SELECT FROM legacy returns
        // zero rows; a re-run on already-seeded data inserts zero rows because the composite PK
        // matches). The cost is one extra `INSERT ... SELECT` round-trip on the steady-state
        // PerSchema path, which is the same cost the first-box-type flip already pays today.
        var needsSeedCheck = !string.Equals(historySchemaFolded, HISTORY_TABLE_SCHEMA, StringComparison.Ordinal);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $@"
CREATE TABLE IF NOT EXISTS {historySchemaQuoted}.""{MIGRATION_HISTORY_TABLE}"" (
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

        // Run the D5 seed on every PerSchema provision (subject to needsSeedCheck above). The seed
        // carries a NOT EXISTS guard against the composite PK so re-fires are harmless — the
        // important invariant is that EVERY box-type that flips gets a chance to seed its own
        // (SchemaName, BoxTableName)-filtered row, not just the first one to land in a freshly
        // created per-schema history table.
        if (needsSeedCheck)
        {
            await SeedHistoryFromLegacyAsync(
                connection, transaction, historySchemaQuoted, resolvedHistorySchema,
                schemaName ?? resolvedHistorySchema, tableName, cancellationToken);
        }
    }

    // EXISTS probe used both for the D5 pre-create check on the per-schema table and the
    // legacy-existence check inside SeedHistoryFromLegacyAsync. The schema parameter must already
    // be folded via PgIdentifier.Normalize — information_schema.tables stores the case PG actually
    // saved, so an unfolded compare would miss tables created from mixed-case identifiers.
    private async Task<bool> DoesHistoryTableExistAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string foldedSchema,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables " +
            "WHERE table_name = @HistoryTableName AND table_schema = @SchemaName)";
        command.Parameters.AddWithValue("@HistoryTableName", MIGRATION_HISTORY_TABLE);
        command.Parameters.AddWithValue("@SchemaName", foldedSchema);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    // Copies this tenant's prior history rows from the legacy public history table into the
    // per-schema history table (ADR 0060 D5). Runs on every PerSchema provision where the resolved
    // history schema differs from the backend default: the per-row NOT EXISTS PK guard makes
    // steady-state runs (no new legacy rows) a zero-row no-op, while still allowing each box-type
    // that flips later to seed its own (SchemaName, BoxTableName) row when needed (PR #4155 fix).
    // Filtered to this tenant (SchemaName + BoxTableName, folded the same way the runner stamped
    // them on the Global INSERT path) so a multi-tenant Global deployment doesn't bleed rows across
    // tenants. All five columns are copied — Description is NOT NULL with no default and AppliedAt
    // preserves the original install/bootstrap timestamp, so the post-flip row is indistinguishable
    // from a row written by the original Global-scope provision. NOT EXISTS on the composite PK makes the
    // seed idempotent. Skipped when no legacy table exists — this is the first-ever provision (no
    // Global predecessor) and there is nothing to copy.
    private async Task SeedHistoryFromLegacyAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string perSchemaQuoted, string perSchemaBare, string boxSchema, string boxTableName,
        CancellationToken cancellationToken)
    {
        const string legacySchema = HISTORY_TABLE_SCHEMA;
        var legacyFolded = PgIdentifier.Normalize(legacySchema);
        var legacyQuoted = PgIdentifier.Quote(legacySchema);
        bool legacyExists;
        try
        {
            legacyExists = await DoesHistoryTableExistAsync(connection, transaction, legacyFolded, cancellationToken);
        }
        catch (PostgresException ex) when (IsLegacyHistoryReadPermissionDenied(ex))
        {
            // PR #4155 reviewer #2: information_schema.tables is row-filtered to objects the role
            // has SOME privilege on (so a missing-grant probe normally just returns zero rows, not
            // 42501), but wrapping with the same when-filter as the INSERT below keeps the
            // documented "any legacy-history-read failure surfaces as a ConfigurationException"
            // contract honest. Non-permission failures (connectivity, syntax) propagate untouched;
            // the outer UoW transaction still rolls back.
            throw BuildLegacyHistoryReadDeniedException(legacyQuoted, perSchemaQuoted, ex);
        }
        if (!legacyExists)
        {
            return;
        }

        // perSchemaQuoted was already AssertSafe-d + Quote-d by the caller. legacySchema is the
        // compile-time const HISTORY_TABLE_SCHEMA ("public") — trivially safe — so no AssertSafe
        // call is needed. If a future refactor turns legacySchema into a non-const derived value
        // (e.g. an operator-configurable legacy-schema override) restore the AssertSafe check.
        // boxSchema is operator-supplied and must be validated before its folded form is passed as a
        // parameter (Normalize doesn't sanitise — AssertSafe does).
        Identifiers.AssertSafe(boxSchema, nameof(boxSchema));

        int rowsCopied;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $@"
INSERT INTO {perSchemaQuoted}.""{MIGRATION_HISTORY_TABLE}""
    (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"", ""AppliedAt"")
SELECT src.""MigrationVersion"", src.""SchemaName"", src.""BoxTableName"", src.""Description"", src.""AppliedAt""
FROM {legacyQuoted}.""{MIGRATION_HISTORY_TABLE}"" AS src
WHERE src.""SchemaName"" = @SchemaName
  AND src.""BoxTableName"" = @BoxTableName
  AND NOT EXISTS (
      SELECT 1 FROM {perSchemaQuoted}.""{MIGRATION_HISTORY_TABLE}"" AS tgt
      WHERE tgt.""SchemaName"" = src.""SchemaName""
        AND tgt.""BoxTableName"" = src.""BoxTableName""
        AND tgt.""MigrationVersion"" = src.""MigrationVersion"")";
            command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(boxSchema));
            command.Parameters.AddWithValue("@BoxTableName", PgIdentifier.Normalize(boxTableName));
            try
            {
                rowsCopied = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsLegacyHistoryReadPermissionDenied(ex))
            {
                // Reviewer #1 hardening (must, not should): any failure reading the legacy history
                // table — typically tenant-isolated credentials lacking SELECT on public — must
                // surface as a ConfigurationException with a clear cause. Silently absorbing the
                // failure would let an empty seed go through; the next provision would then
                // bootstrap-stamp a fresh row, effectively re-running the migration ledger and
                // breaking FR5. The throw rolls back the surrounding transaction so no partial
                // per-schema state is left behind.
                //
                // PR #4155 reviewer #2 tightening: the catch is filtered to the SELECT-permission
                // SqlState (42501 / insufficient_privilege) so a deadlock victim, connection drop,
                // statement timeout, or future-refactor syntax error doesn't get misreported as
                // "grant SELECT on the legacy table". Unmatched PostgresExceptions propagate
                // unchanged; the outer UoW transaction still rolls back.
                throw BuildLegacyHistoryReadDeniedException(legacyQuoted, perSchemaQuoted, ex);
            }
        }

        if (rowsCopied > 0)
        {
            // Spec 0029 NF5/AC7 (ADR 0060 D6, reviewer #3): structured fields RowCount + BoxTable +
            // LegacySchema + TargetSchema each appear as a separate placeholder so a structured log
            // sink can filter on individual fields. Target/legacy schemas are passed bare (without
            // the PgIdentifier.Quote double-quote) so the values are stable join keys against other
            // operator dashboards that report unquoted schema identifiers. The existing legacy-
            // history-seeded Activity event additionally carries the row count as a tag so a
            // trace-store query can size flip impact without parsing event names.
            Logger.LogInformation(
                "Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}",
                rowsCopied, boxTableName, legacySchema, perSchemaBare);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventLegacyHistorySeeded,
                default,
                new ActivityTagsCollection
                {
                    { BrighterSemanticConventions.BoxMigrationSeedRowCount, rowsCopied }
                }));
        }
    }

    // PR #4155 reviewer #2: narrow the seed catch so the operator-facing "grant SELECT on the
    // legacy history table" message only fires when the provider actually raised a SELECT-
    // permission error. 42501 (insufficient_privilege) is the canonical SqlState PostgreSQL
    // raises for missing SELECT/INSERT on a relation. Other PostgresExceptions (connectivity
    // 08xxx, deadlock 40P01, statement_timeout 57014, future-refactor syntax errors 42xxx-other)
    // propagate untouched.
    private static bool IsLegacyHistoryReadPermissionDenied(PostgresException ex) =>
        ex.SqlState == "42501";

    private static ConfigurationException BuildLegacyHistoryReadDeniedException(
        string legacyQuoted, string perSchemaQuoted, Exception inner) =>
        new ConfigurationException(
            $"Brighter PerSchema migration: every provision run reads the legacy default-schema " +
            $"history table {legacyQuoted}.\"{MIGRATION_HISTORY_TABLE}\" so any unseeded tenant " +
            $"rows can be copied into the per-schema history (the per-row NOT EXISTS guard makes " +
            $"steady-state runs a zero-row no-op, but the SELECT against the legacy table runs " +
            $"every time). Grant the runner SELECT on that table (and INSERT on " +
            $"{perSchemaQuoted}.\"{MIGRATION_HISTORY_TABLE}\") for the lifetime of the PerSchema " +
            $"deployment and retry. The original provider exception is preserved as the inner " +
            $"exception.",
            inner);

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
                migration.Version, migration.Description.Value, cancellationToken);
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
                migration.Version, migration.Description.Value, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, transaction, migration.UpScript.Value, cancellationToken);

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
