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
/// with the same exemption shape used by <c>RelationalBoxMigrationRunnerBase</c>.
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
    private readonly IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> _detectionHelper;
    private readonly IAmABoxMigrationCatalog _catalog;
    private readonly IAmABoxPayloadModeValidator<TConnection> _payloadValidator;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    /// <summary>
    /// Initialises the base. Derived classes forward their canonical 5-arg ctor (from the Phase 8
    /// ctor cascade) plus the per-derivation <see cref="BoxProvisioning.BoxType"/> value to this
    /// ctor; the back-compat 2-arg ctor on each derivation continues to chain via <c>this(...)</c>
    /// to the 5-arg ctor (NF10 — no call-site change).
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
        var migrations = _catalog.All(_configuration);
        var tableState = await DetectTableStateAsync(migrations, cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await _migrationRunner.MigrateAsync(
            BoxTableName,
            _configuration.SchemaName,
            BoxType,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        // Sync `using` for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so `await using` would not compile across the shared-assembly
        // TFM matrix. Mirrors the §B.2 precedent at RelationalBoxMigrationRunnerBase.cs:112-116.
        using var connection = CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await _detectionHelper.DoesHistoryExistAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
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
                CurrentVersion: ClampDetectedVersion(detectedVersion));
        }

        var maxVersion = await _detectionHelper.GetMaxVersionAsync(
            connection, BoxTableName, EffectiveSchemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        // Sync `using` for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so `await using` would not compile across the shared-assembly
        // TFM matrix. Mirrors the §B.2 precedent at RelationalBoxMigrationRunnerBase.cs:112-116.
        using var connection = CreateConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await _payloadValidator.ValidateAsync(
            connection, BoxTableName, EffectiveSchemaName,
            PayloadColumnName, _configuration.BinaryMessagePayload, cancellationToken);
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

    /// <summary>
    /// Post-process the version inferred by
    /// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}.DetectCurrentVersionAsync"/>.
    /// Default clamps negative values (e.g. spec 0027's <c>-1</c> "discriminator missing"
    /// sentinel) to zero — the pre-lock value is a hint and the runner is authoritative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Transitional — removed in Phase 13.B.</b> This hook exists solely to host MySQL's
    /// no-clamp override during Phase 13.A (NF9 — bit-for-bit behavioural neutrality of the
    /// structural pull-up). Phase 13.B (F11) unifies MySQL's behaviour with the other three
    /// relational backends and, in the same commit, removes both <c>MySql*Provisioner</c>'s
    /// override AND this hook — inlining <c>detectedVersion &lt; 0 ? 0 : detectedVersion</c>
    /// directly into <see cref="DetectTableStateAsync"/>. After 13.B no derivation overrides
    /// this method, so the hook earns no keep; preserving it post-13.B would be speculative
    /// generality. A future backend with a richer sentinel set can re-introduce a hook in
    /// a follow-up spec without regret.
    /// </para>
    /// </remarks>
    /// <param name="detectedVersion">The version inferred by the detection helper for a
    /// pre-existing table without history rows.</param>
    /// <returns>The clamped or pass-through version to record on the
    /// <see cref="BoxTableState"/> returned from <see cref="DetectTableStateAsync"/>.</returns>
    protected virtual int ClampDetectedVersion(int detectedVersion) =>
        detectedVersion < 0 ? 0 : detectedVersion;
}
