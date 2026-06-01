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

using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Abstract base class for the eight relational <see cref="IAmABoxProvisioner"/> implementations
/// (MSSQL/PostgreSQL/MySQL/SQLite × Outbox/Inbox). Owns the orchestration body
/// (<see cref="ProvisionAsync"/> + pre-lock detection + payload-mode validation); derived classes
/// supply only the irreducibly-backend-specific hooks (connection factory and payload column name).
/// </summary>
/// <remarks>
/// Spanner's pair (<c>SpannerOutboxProvisioner</c>, <c>SpannerInboxProvisioner</c>) does NOT
/// derive from this base — the ctor requires
/// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}"/>, which Spanner
/// cannot honestly implement (no V_k chain per ADR 0057 §6 / §A.1). Spanner stays free-standing
/// with the same exemption shape used by <c>SqlBoxMigrationRunner</c>.
/// </remarks>
/// <typeparam name="TConnection">The backend-specific <see cref="DbConnection"/> subtype
/// (e.g. <c>SqlConnection</c>, <c>NpgsqlConnection</c>, <c>MySqlConnection</c>, <c>SqliteConnection</c>).</typeparam>
/// <typeparam name="TTransaction">The backend-specific <see cref="DbTransaction"/> subtype
/// (e.g. <c>SqlTransaction</c>, <c>NpgsqlTransaction</c>, <c>MySqlTransaction</c>, <c>SqliteTransaction</c>).</typeparam>
public abstract class SqlBoxProvisioner<TConnection, TTransaction>
    : IAmABoxProvisioner
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    // Static logger keeps the ctor surface unchanged across the 8 concrete provisioners; only
    // exercised by the pre-lock-hint failure swallow below (Spec 0029 T-PERM).
    private static readonly ILogger s_logger =
        ApplicationLogging.CreateLogger<SqlBoxProvisioner<TConnection, TTransaction>>();

    private readonly IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> _detectionHelper;
    private readonly IAmABoxMigrationCatalog _catalog;
    private readonly IAmABoxPayloadModeValidator<TConnection> _payloadValidator;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    /// <summary>
    /// Initialises the base. Derived classes forward their 5-arg ctor plus the per-derivation
    /// <see cref="BoxProvisioning.BoxType"/> value to this ctor.
    /// </summary>
    protected SqlBoxProvisioner(
        IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<TConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner,
        BoxType boxType)
    {
        _detectionHelper = detectionHelper;
        _catalog = catalog;
        _payloadValidator = payloadValidator;
        _configuration = configuration;
        _migrationRunner = migrationRunner;
        BoxType = boxType;
    }

    /// <inheritdoc />
    public BoxType BoxType { get; }

    /// <inheritdoc />
    public string BoxTableName => BoxType == BoxType.Outbox
        ? _configuration.OutBoxTableName
        : _configuration.InBoxTableName;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        // Defence-in-depth at the framework chokepoint. Catalogs gate AssertSafe at the entry to
        // All(...), but the provisioner opens connections and runs detection BEFORE the catalog
        // returns, so an unsafe identifier from configuration reaches the live DB connection
        // unless the provisioner guards at its own entry. _configuration.SchemaName is nullable:
        // SQLite has no schema concept per ADR 0057 §6, so a null value is legitimate.
        var boxTableNameParam = BoxType == BoxType.Outbox
            ? nameof(IAmARelationalDatabaseConfiguration.OutBoxTableName)
            : nameof(IAmARelationalDatabaseConfiguration.InBoxTableName);
        Identifiers.AssertSafe(BoxTableName, boxTableNameParam);
        if (_configuration.SchemaName is not null)
        {
            Identifiers.AssertSafe(
                _configuration.SchemaName, nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }

        var migrations = _catalog.All(_configuration);

        // Sync `using` for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so `await using` would not compile across the shared-assembly
        // TFM matrix. Mirrors the §B.2 precedent at SqlBoxMigrationRunner.cs:112-116.
        //
        // Detection and validation run sequentially against the same open
        // connection, so we cut one connection round-trip per provisioning run (and avoid the
        // connection-pool churn in deployments with strict per-pod connection caps).
        BoxTableState tableState;
        using (var connection = CreateConnection(_configuration.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            tableState = await DetectTableStateAsync(connection, migrations, cancellationToken);

            if (tableState.TableExists)
            {
                await _payloadValidator.ValidateAsync(
                    connection, BoxTableName, EffectiveSchemaName,
                    PayloadColumnName, _configuration.BinaryMessagePayload, cancellationToken);
            }
        }

        // The runner sources its migration chain from its own injected
        // IAmABoxMigrationCatalog (see SqlBoxMigrationRunner ctor) — the provisioner no longer
        // forwards the list. The provisioner still calls _catalog.All(...) above so the pre-lock
        // detection helper can infer current version from the column set.
        await _migrationRunner.MigrateAsync(
            BoxTableName,
            _configuration.SchemaName,
            BoxType,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        TConnection connection,
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        // Pre-lock read is an explicitly discarded hint (the runner re-detects authoritatively
        // under the lock and resolves the history schema there), so this site passes
        // historySchema: null = backend default — see ADR 0060 D4. A provider exception on the
        // hint is most commonly tenant-isolated credentials lacking SELECT on the backend-default
        // history schema (Spec 0029 FR5 / T-PERM). Swallow it: the runner's under-lock detection
        // resolves the per-schema history under PerSchema scope and its D5 seed converts any
        // legacy-read failure into a ConfigurationException with the documented operator-facing
        // message. Logged at Debug so the swallow is observable in diagnostics. DbException is
        // the common base for SqlException / NpgsqlException / MySqlException / SqliteException
        // and the only failure family the hint genuinely cannot reason about.
        bool historyExists;
        try
        {
            historyExists = await _detectionHelper.DoesHistoryExistAsync(
                connection, BoxTableName, EffectiveSchemaName, historySchema: null, cancellationToken);
        }
        catch (DbException ex)
        {
            s_logger.LogDebug(ex,
                "Pre-lock historyExists hint for '{Schema}.{Table}' unavailable; deferring to the runner's under-lock authoritative detection.",
                EffectiveSchemaName, BoxTableName);
            historyExists = false;
        }

        if (!historyExists)
        {
            // Pre-lock detection is a hint for the caller; the runner re-detects under the lock.
            // Negative or zero return values are not gated here — the runner is the single source
            // of truth for discriminator violations and unknown-schema rejections.
            var detectedVersion = await _detectionHelper.DetectCurrentVersionAsync(
                connection, BoxTableName, EffectiveSchemaName,
                BoxType, migrations, cancellationToken);
            return new BoxTableState(
                TableExists: true, HistoryExists: false,
                CurrentVersion: detectedVersion < 0 ? 0 : detectedVersion);
        }

        // Same hint-swallow rationale as historyExists above: a provider failure here is a hint
        // signal only, the runner re-detects authoritatively.
        int maxVersion;
        try
        {
            maxVersion = await _detectionHelper.GetMaxVersionAsync(
                connection, BoxTableName, EffectiveSchemaName, historySchema: null, cancellationToken);
        }
        catch (DbException ex)
        {
            s_logger.LogDebug(ex,
                "Pre-lock maxVersion hint for '{Schema}.{Table}' unavailable; deferring to the runner's under-lock authoritative detection.",
                EffectiveSchemaName, BoxTableName);
            maxVersion = 0;
        }
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    /// <summary>
    /// Backend-specific connection factory. Implementations typically return
    /// <c>new {Backend}Connection(connectionString)</c>.
    /// </summary>
    /// <param name="connectionString">The connection string from
    /// <see cref="IAmARelationalDatabaseConfiguration.ConnectionString"/>.</param>
    protected abstract TConnection CreateConnection(string connectionString);

    /// <summary>
    /// The payload column name to validate against the configured payload mode.
    /// Outbox: <c>"Body"</c> (most backends) or <c>"body"</c> (PostgreSQL lower-case convention).
    /// Inbox: <c>"CommandBody"</c> / <c>"commandbody"</c>.
    /// </summary>
    protected abstract string PayloadColumnName { get; }

    /// <summary>
    /// The schema name to pass to the detection helper and payload validator. Defaults to the
    /// configured schema; SQLite overrides to <c>null</c> (no schema concept per ADR 0057 §6).
    /// The runner call inside <see cref="ProvisionAsync"/> always uses
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/> directly — only the
    /// detection-helper and payload-validator calls observe this property.
    /// </summary>
    protected virtual string? EffectiveSchemaName => _configuration.SchemaName;
}
