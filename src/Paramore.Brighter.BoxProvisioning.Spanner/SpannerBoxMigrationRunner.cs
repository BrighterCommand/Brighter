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
///   <item><description>Existing table without history (bootstrap): verifies the discriminator column (<c>HeaderBag</c> for outbox / <c>CommandBody</c> for inbox); absence throws <see cref="ConfigurationException"/>; presence stamps <c>V_latest</c> with the ADR 0057 §6 "no known legacy installations" description.</description></item>
///   <item><description>Existing table with history (normal): compares <c>MAX(V)</c> to <c>V_latest</c>; equality is a no-op; <c>MAX(V) &gt; V_latest</c> throws <see cref="ConfigurationException"/>; <c>MAX(V) &lt; V_latest</c> is undefined per ADR 0057 §6 (manual recovery required) and throws the same out-of-sync error.</description></item>
/// </list>
/// <para>
/// Unlike the relational runners, this runner takes no advisory lock and does not re-detect the
/// caller-supplied <see cref="BoxTableState"/>. Concurrent provisioners are safe without one:
/// Spanner serialises DDL, replayed <c>CREATE TABLE IF NOT EXISTS</c> DDL tolerates "already
/// exists", and the history row is keyed on <c>(BoxTableName, MigrationVersion)</c>, so a racing
/// process cannot double-stamp.
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

    // Must equal the relational migration chain lengths (the PostgreSQL inbox is one behind, per
    // ADR 0057 §1); the drift test in tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning
    // enforces it. static readonly, not const, so a new value is not baked into consumers' IL.
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
        Identifiers.AssertSafe(tableName.Value, nameof(tableName));

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

    // The outbox carries a second statement, the CausationId index, which Spanner cannot express
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

    private static void VerifyAtLatestVersionOrThrow(string tableName, int vLatest, int currentVersion)
    {
        if (currentVersion == vLatest) return;

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

    // Swallowing FailedPrecondition is safe only because every caller passes builder-generated
    // CREATE TABLE IF NOT EXISTS DDL (ADR 0057 §3, cross-box history-table race).
    private async Task ExecuteCreateTableIfNotExistsSafeAsync(
        SpannerConnection connection, string[] ddlStatements,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = connection.CreateDdlCommand(ddlStatements[0], ddlStatements[1..]);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SpannerException ex) when (IsAlreadyExists(ex))
        {
        }
        catch (SpannerException ex) when (IsConcurrentSchemaChange(ex))
        {
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
        catch (SpannerException ex) when (IsAlreadyExists(ex))
        {
            _logger.LogDebug(ex,
                "{HistoryTable} already created by racing session (Spanner ALREADY_EXISTS)",
                MigrationHistoryTable);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
        catch (SpannerException ex) when (IsConcurrentSchemaChange(ex))
        {
            _logger.LogDebug(ex,
                "{HistoryTable} concurrent schema change absorbed (Spanner FAILED_PRECONDITION on CREATE IF NOT EXISTS)",
                MigrationHistoryTable);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
    }

    private static bool IsAlreadyExists(SpannerException ex) =>
        ex.RpcException.StatusCode == StatusCode.AlreadyExists;

    // The emulator reports the loser of concurrent CREATE TABLE IF NOT EXISTS as FailedPrecondition.
    private static bool IsConcurrentSchemaChange(SpannerException ex) =>
        ex.RpcException.StatusCode == StatusCode.FailedPrecondition;

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

    // Replicas race between IsMigrationAppliedAsync and this insert; the loser hits AlreadyExists
    // on the (BoxTableName, MigrationVersion) key (ADR 0057 §6).
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
        catch (SpannerException ex) when (IsAlreadyExists(ex))
        {
        }
    }
}
