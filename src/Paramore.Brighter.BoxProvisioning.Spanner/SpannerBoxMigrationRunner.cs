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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Inbox.Spanner;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Runs box migrations against a Spanner database. DDL operations use
/// <c>CreateDdlCommand</c> and are separate from read-write transactions.
/// Spanner handles DDL concurrency internally — no advisory lock is needed.
/// "Already exists" errors on DDL are caught for crash safety.
/// </summary>
/// <remarks>
/// Per ADR 0057 §6 the Spanner runner is degenerate (fresh-only) — no V_k chain and no
/// <see cref="IAmABoxMigrationCatalog"/>; V_latest is hard-coded via <see cref="VLatestOutbox"/> /
/// <see cref="VLatestInbox"/> in sync with the relational chains, and the fresh-install DDL
/// comes from the per-backend builders rather than a catalog hook.
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
/// not interleave; (b) <c>ExecuteCreateTableIfNotExistsSafeAsync</c> swallows <c>AlreadyExists</c> /
/// <c>FailedPrecondition</c> on DDL replay (crash safety); (c) the history insert is gated by
/// <c>IsMigrationAppliedAsync</c> against the PK <c>(BoxTableName, MigrationVersion)</c>, so a
/// racing process cannot double-stamp. No application-level lock is therefore required.
/// </para>
/// </remarks>
public class SpannerBoxMigrationRunner : IAmABoxMigrationRunner
{
    // Spanner rejects identifiers starting with `_` (reserved for system objects),
    // so this backend uses `BrighterMigrationHistory` while other backends use
    // `__BrighterMigrationHistory`.
    internal const string MigrationHistoryTable = "BrighterMigrationHistory";

    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction> _detectionHelper;
    private readonly IAmABrighterTracer? _tracer;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the runner with an explicit detection helper. Spanner is degenerate per
    /// ADR 0057 §6 (no V_k chain), so the BASE detection-helper interface is sufficient —
    /// no version inference is performed.
    /// </summary>
    public SpannerBoxMigrationRunner(
        IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction> detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABrighterTracer? tracer = null,
        ILogger? logger = null)
    {
        _detectionHelper = detectionHelper;
        _configuration = configuration;
        _tracer = tracer;
        _logger = logger ?? ApplicationLogging.CreateLogger<SpannerBoxMigrationRunner>();
    }

    /// <summary>
    /// Convenience ctor used by integration tests: synthesises a default
    /// <see cref="SpannerBoxDetectionHelper"/> so test arrange blocks don't have to
    /// construct one explicitly.
    /// </summary>
    public SpannerBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABrighterTracer? tracer = null,
        ILogger? logger = null)
        : this(new SpannerBoxDetectionHelper(), configuration, tracer, logger)
    {
    }

    // IMPORTANT: keep these in sync with the relational chain length —
    //   VLatestOutbox === new MySqlOutboxMigrationCatalog().All(...).Count
    //                 === new MsSqlOutboxMigrationCatalog().All(...).Count
    //                 === new PostgreSqlOutboxMigrationCatalog().All(...).Count
    //                 === new SqliteOutboxMigrationCatalog().All(...).Count
    //   VLatestInbox === new MsSqlInboxMigrationCatalog().All(...).Count
    //                === new MySqlInboxMigrationCatalog().All(...).Count
    //                === new SqliteInboxMigrationCatalog().All(...).Count
    //                  (PostgreSQL inbox is one version behind the other three — ADR 0057 §E,
    //                   the PG inbox was born post-ContextKey-era so its V1 already includes the
    //                   column the other three add at V2. Spec 0027 (#2541) adds CausationId to
    //                   every relational inbox, advancing MsSql/MySql/Sqlite V2→V3 and PG V1→V2,
    //                   so PG stays exactly one behind and the cross-backend test carves it out.)
    // Spanner has no V_k chain (ADR 0057 §6 — fresh-install-only), so the latest version is
    // effectively a stamp on a freshly-built table. When a relational backend advances to
    // V8/V3 etc., bump these values so Spanner's history row keeps the same V_latest as
    // its relational siblings — the cross-backend drift test in
    // tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning enforces this parity.
    //
    // Exposed as public to allow the cross-backend drift test (which lives in the Spanner test
    // assembly, not the BoxProvisioning.Spanner assembly) to compare against the relational
    // catalog counts without strong-name InternalsVisibleTo gymnastics.
    //
    // static readonly (not const): the value is read at runtime from this assembly rather than
    // baked into IL at every call site at compile time, so downstream consumers that reference
    // this assembly pick up a new V_latest after a recompile of *this* assembly alone. With
    // `const` the old value would persist in downstream IL until every consumer also rebuilt,
    // letting an out-of-date V_latest silently flow through a partial-rebuild deployment.
    public static readonly int VLatestOutbox = 8;
    public static readonly int VLatestInbox = 3;

    private const string BootstrapDescription =
        "bootstrap: spanner-assumed-current (no known legacy installations, A-2)";

    /// <inheritdoc />
    public async Task MigrateAsync(
        BoxTableName tableName,
        SchemaName? schemaName,
        BoxType boxType,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        // Defence in depth (ADR 0057 §1, spec 0027 PR #4039 review #46-3 #4): Spanner has no
        // *Migrations.All(...) factory, so this is the only place to reject unsafe identifiers
        // before they reach BuildBoxDdl / BootstrapExistingTableAsync's information_schema probe.
        Identifiers.AssertSafe(tableName.Value, nameof(tableName));

        _ = schemaName; // Spanner does not use schemas; the configuration's database is implicit.

        // Mirror the relational base runner's instrumentation shape (per ADR 0057 §6 Spanner
        // skips the advisory-lock / re-detection orchestration, but the operator-facing span
        // still carries the same tag + child-event vocabulary so a multi-backend startup trace
        // reads consistently).
        using var activity = StartMigrationActivity(tableName.Value, schemaName?.Value, boxType);

        using var connection = SpannerConnectionHelper.CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventEnsureHistory));
            await EnsureHistoryTableAsync(connection, cancellationToken);

            var vLatest = LatestVersionFor(boxType);

            if (!tableState.TableExists)
            {
                activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "fresh");
                activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventFreshInstall));
                await FreshInstallAsync(connection, tableName.Value, boxType, vLatest, cancellationToken);
            }
            else if (!tableState.HistoryExists)
            {
                activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "bootstrap");
                activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventBootstrap));
                await BootstrapExistingTableAsync(connection, tableName.Value, boxType, vLatest, cancellationToken);
            }
            else
            {
                activity?.SetTag(BrighterSemanticConventions.BoxMigrationPath, "normal");
                activity?.AddEvent(new ActivityEvent(BrighterSemanticConventions.BoxMigrationEventNormalUpdate));
                VerifyAtLatestVersionOrThrow(tableName.Value, vLatest, tableState.CurrentVersion);
            }
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private Activity? StartMigrationActivity(string tableName, string? schemaName, BoxType boxType)
    {
        var activity = _tracer?.ActivitySource.StartActivity(
            $"{BrighterSemanticConventions.BoxMigration} {tableName}",
            ActivityKind.Internal);
        if (activity is null) return null;
        activity.SetTag(BrighterSemanticConventions.DbSystem, DbSystem.Spanner.ToDbName());
        activity.SetTag(BrighterSemanticConventions.DbTable, tableName);
        if (schemaName is not null)
        {
            activity.SetTag(BrighterSemanticConventions.DbNamespace, schemaName);
        }
        activity.SetTag(BrighterSemanticConventions.BoxType, boxType.ToString());
        return activity;
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
        await ExecuteCreateTableIfNotExistsSafeAsync(connection, builderDdl, cancellationToken);

        if (await IsMigrationAppliedAsync(connection, tableName, vLatest, cancellationToken))
            return;

        await InsertHistoryRowToleratingDuplicateAsync(
            connection, tableName, vLatest, $"fresh install at V{vLatest}", cancellationToken);
    }

    // Returns the fresh-install DDL statements for the box. The outbox carries a second
    // statement — the CausationId replay index (Spec 0027, #2541) — which Spanner cannot express
    // inline in the CREATE TABLE, so it is batched alongside the table create.
    private string[] BuildBoxDdl(BoxType boxType, string tableName) => boxType switch
    {
        BoxType.Outbox =>
        [
            SpannerOutboxBuilder.GetDDL(tableName, _configuration.BinaryMessagePayload),
            SpannerOutboxBuilder.GetCausationIndexDDL(tableName)
        ],
        BoxType.Inbox => [SpannerInboxBuilder.GetDDL(tableName)],
        _ => throw new ArgumentOutOfRangeException(nameof(boxType), boxType, "Unsupported box type")
    };

    private async Task BootstrapExistingTableAsync(
        SpannerConnection connection, string tableName, BoxType boxType, int vLatest,
        CancellationToken cancellationToken)
    {
        var columns = await _detectionHelper.GetTableColumnsAsync(
            connection, tableName, schemaName: null, cancellationToken);

        var discriminator = _detectionHelper.DiscriminatorFor(boxType);
        if (!columns.Contains(discriminator))
        {
            throw new ConfigurationException(
                $"Table '{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (await IsMigrationAppliedAsync(connection, tableName, vLatest, cancellationToken))
            return;

        await InsertHistoryRowToleratingDuplicateAsync(
            connection, tableName, vLatest, BootstrapDescription, cancellationToken);
    }

    // Per PR #4039 reviewer item M2-10: the previous name `RunNormalPath` read oddly inside
    // MigrateAsync because the method only validates (and throws on mismatch) — it does not
    // "run" anything when the version is current. The rename makes the may-throw semantics
    // explicit at the call site. The early `currentVersion == vLatest` return is the
    // expected post-bootstrap steady state; the throw is reserved for the strict
    // history-divergence case Spanner has no in-place migration path to fix.
    private static void VerifyAtLatestVersionOrThrow(string tableName, int vLatest, int currentVersion)
    {
        if (currentVersion == vLatest) return;

        // Per ADR 0057 §6: Spanner has no advisory-lock concept and no in-place migration
        // chain — fresh-install is the only forward path, and the relational backends'
        // bootstrap-at-V_k re-run is intentionally not available here. Surface the exact
        // recovery action in the message so the operator does not have to cross-reference
        // the ADR and reverse-engineer the history-table shape.
        throw new ConfigurationException(
            $"Migration list out of sync for table '{tableName}': " +
            $"installed V={currentVersion}, expected V={vLatest}. " +
            $"Spanner does not support in-place migration (ADR 0057 §6). Recovery: after " +
            $"verifying the table schema matches V={vLatest} in the relevant builder " +
            $"(SqlSpannerOutboxBuilder / SqlSpannerInboxBuilder — see Drift tests), insert " +
            $"a synthetic history row into `{MigrationHistoryTable}` for this table so the " +
            $"next provisioner run no-ops, e.g.: " +
            $"INSERT INTO `{MigrationHistoryTable}` (`BoxTableName`, `MigrationVersion`, " +
            $"`Description`, `AppliedAt`) VALUES ('{tableName}', {vLatest}, " +
            $"'Manual recovery — schema verified at V={vLatest}', PENDING_COMMIT_TIMESTAMP()).");
    }

    // PR #4039 review item #7 originally narrowed this catch to AlreadyExists only, on the
    // grounds that FailedPrecondition is a Spanner catch-all that also covers schema drift,
    // invalid options, and many cases beyond "table/column already exists". That narrowing
    // still protects callers that pass arbitrary DDL — but every current caller passes a
    // `CREATE TABLE IF NOT EXISTS` produced by a per-backend builder against a known box-table
    // shape, and on that DDL shape FailedPrecondition is the emulator's surfacing of concurrent
    // schema-change serialisation (real Spanner serialises and the loser sees AlreadyExists).
    // Both racers run the identical DDL against the identical schema, so the post-condition
    // (box table present with the expected columns) holds whichever racer wins.
    //
    // Schema-drift detection is provided by the per-backend Drift tests (V_latest column-set
    // equality across builders and migration chains) — those would surface a real drift before
    // it ever reached the swallow here. Invalid-options drift would surface deterministically
    // on every replica, not as a flake. The combined catch keeps the original AlreadyExists
    // arm strict and adds a FailedPrecondition arm scoped to CREATE TABLE IF NOT EXISTS DDL.
    private async Task ExecuteCreateTableIfNotExistsSafeAsync(
        SpannerConnection connection, string[] ddlStatements,
        CancellationToken cancellationToken)
    {
        try
        {
            // CreateDdlCommand batches the CREATE TABLE IF NOT EXISTS with any trailing
            // statements (e.g. the outbox CausationId index) so they apply together.
            var command = connection.CreateDdlCommand(ddlStatements[0], ddlStatements[1..]);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.AlreadyExists)
        {
            // Box table already exists — safe to continue (crash between DDL and history write).
            // Operational visibility for the actual history-table race is provided by
            // EnsureHistoryTableAsync's dedicated catch below.
            _ = ex; // explicit no-op acknowledges the swallow
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.FailedPrecondition)
        {
            // Concurrent fresh-installers landed their box-table CREATE TABLE IF NOT EXISTS
            // inside the same Spanner schema-change window. The emulator returns the loser
            // FailedPrecondition ("concurrent schema change operation in progress") before
            // resolving to AlreadyExists; real Spanner serialises and the loser sees the
            // AlreadyExists arm above. Post-condition is identical either way.
            _logger.LogDebug(ex,
                "Box-table CREATE TABLE IF NOT EXISTS race absorbed (Spanner FAILED_PRECONDITION)");
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
    }

    private async Task EnsureHistoryTableAsync(
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

        try
        {
            var command = connection.CreateDdlCommand(ddl);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.AlreadyExists)
        {
            // TOCTOU on Spanner's information_schema: another connection raced our CREATE TABLE
            // IF NOT EXISTS between Spanner's internal existence check and the DDL commit. The
            // history table now exists with the schema we intended (the racing session ran the
            // same DDL), which is the post-condition we wanted.
            //
            // Surface the swallow so operators investigating "did two replicas race here?" have
            // a signal — Debug-level log + Activity event on the migration span (when one is
            // active). Silent swallow was the prior behaviour; per PR #4039 review item #7 the
            // race is real and the swallow is intentional, but operators got no signal that
            // two racers serialised here.
            _logger.LogDebug(ex,
                "{HistoryTable} already created by racing session (Spanner ALREADY_EXISTS)",
                MigrationHistoryTable);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.FailedPrecondition)
        {
            // Concurrent fresh-installers can land their CREATE TABLE IF NOT EXISTS DDL on the
            // history table inside the same Spanner schema-change window. The emulator surfaces
            // the loser as FailedPrecondition ("Schema change operation rejected because a
            // concurrent schema change operation or read-write transaction is already in
            // progress") before resolving to ALREADY_EXISTS; real Spanner serialises and the
            // loser sees ALREADY_EXISTS. The DDL is `CREATE TABLE IF NOT EXISTS` on a fixed
            // history table whose schema is identical across racers, so the post-condition
            // (history table present, with the expected shape) holds either way once the racing
            // session commits. The mirror catch on ExecuteCreateTableIfNotExistsSafeAsync covers the same race on
            // the per-backend box-table DDL — see the rationale block on that method.
            _logger.LogDebug(ex,
                "{HistoryTable} concurrent schema change absorbed (Spanner FAILED_PRECONDITION on CREATE IF NOT EXISTS)",
                MigrationHistoryTable);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
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

        // SELECT COUNT(1) is one of the few SQL expressions guaranteed never to return NULL
        // (per ANSI SQL — COUNT over zero rows returns 0, not NULL). The pattern-match form
        // mirrors the MSSQL/PG detection-helper style: a future driver bug returning null
        // surfaces as a named InvalidOperationException rather than a bare NRE. Per PR #4039
        // reviewer item F2-8.
        var raw = await command.ExecuteScalarAsync(cancellationToken);
        var count = raw is long n
            ? n
            : throw new InvalidOperationException(
                $"IsMigrationAppliedAsync: COUNT(1) over {MigrationHistoryTable} for table '{tableName}' version {version} returned null.");
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

    // The IsMigrationAppliedAsync check + the subsequent history-row INSERT form a TOCTOU
    // pair on Spanner: two concurrent replicas (fresh-installing or bootstrapping the same
    // existing-without-history table) can both pass the existence check (commit timestamps
    // from the winner's eventual insert have not yet propagated to the loser's read snapshot
    // — Spanner's history-row insert uses SpannerParameter.CommitTimestamp, so visibility
    // lags), then both attempt the insert; the loser hits AlreadyExists on the PK
    // (BoxTableName, MigrationVersion). Mirror ExecuteCreateTableIfNotExistsSafeAsync's filter shape and absorb
    // the benign race.
    private static async Task InsertHistoryRowToleratingDuplicateAsync(
        SpannerConnection connection, string tableName,
        int version, string description,
        CancellationToken cancellationToken)
    {
        try
        {
            await InsertHistoryRowAsync(
                connection, tableName, version, description, cancellationToken);
        }
        catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.AlreadyExists)
        {
            // Concurrent caller already stamped this version — benign race.
        }
    }
}
