using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Provisions a Spanner outbox table. Performs a pre-lock detection pass to gate payload-mode
/// validation, then delegates to <see cref="IAmABoxMigrationRunner"/>. Per ADR 0057 §6 the
/// Spanner box surface is degenerate (fresh-install only, no V_k chain), so this provisioner
/// uses the BASE <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}"/>
/// interface and is exempt from <see cref="IAmABoxMigrationCatalog"/>. Spanner has no
/// schema concept; <c>schemaName</c> is passed as <c>null</c> throughout.
/// </summary>
public class SpannerOutboxProvisioner : IAmABoxProvisioner
{
    private static readonly HashSet<string> V1Columns = new(StringComparer.Ordinal)
    {
        "MessageId", "Topic", "MessageType", "Timestamp", "CorrelationId",
        "ReplyTo", "ContentType", "PartitionKey", "Dispatched", "HeaderBag",
        "Body", "Source", "Type", "DataSchema", "Subject",
        "TraceParent", "TraceState", "Baggage", "WorkflowId", "JobId",
        "DataRef", "SpecVersion"
    };

    private readonly IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction> _detectionHelper;
    private readonly IAmABoxPayloadModeValidator<SpannerConnection> _payloadValidator;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    /// <summary>
    /// Canonical ctor — Phase 8.5 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics. Spanner is
    /// degenerate per ADR 0057 §6, so the role interface is the BASE
    /// <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}"/> and no
    /// <see cref="IAmABoxMigrationCatalog"/> is injected.
    /// </summary>
    public SpannerOutboxProvisioner(
        IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction> detectionHelper,
        IAmABoxPayloadModeValidator<SpannerConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _detectionHelper = detectionHelper;
        _payloadValidator = payloadValidator;
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises default singletons for the
    /// two role-interface dependencies; removed when the DI cascade lands in Phase 9.
    /// </summary>
    public SpannerOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    public BoxType BoxType => BoxType.Outbox;
    public string BoxTableName => _configuration.OutBoxTableName;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var tableState = await DetectTableStateAsync(cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        // Per ADR 0057 §6 the Spanner runner is degenerate (fresh-only), so it
        // ignores the migrations parameter; pass an empty list to satisfy the
        // IAmABoxMigrationRunner contract.
        await _migrationRunner.MigrateAsync(
            _configuration.OutBoxTableName,
            _configuration.SchemaName,
            BoxType.Outbox,
            Array.Empty<IAmABoxMigration>(),
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(CancellationToken cancellationToken)
    {
        using var connection = SpannerConnectionHelper.CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, _configuration.OutBoxTableName, schemaName: null, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await _detectionHelper.DoesHistoryExistAsync(
            connection, _configuration.OutBoxTableName, schemaName: null, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await DetectCurrentVersionAsync(
                connection, _configuration.OutBoxTableName, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await _detectionHelper.GetMaxVersionAsync(
            connection, _configuration.OutBoxTableName, schemaName: null, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        using var connection = SpannerConnectionHelper.CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await _payloadValidator.ValidateAsync(
            connection, _configuration.OutBoxTableName, schemaName: null,
            "Body", _configuration.BinaryMessagePayload, cancellationToken);
    }

    private async Task<int> DetectCurrentVersionAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        var actualColumns = await _detectionHelper.GetTableColumnsAsync(
            connection, tableName, schemaName: null, cancellationToken);
        return V1Columns.IsSubsetOf(actualColumns) ? 1 : 0;
    }
}
