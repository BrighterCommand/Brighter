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
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Provisions a PostgreSQL outbox table. Performs a pre-lock detection pass to gate payload-mode
/// validation, then delegates to <see cref="IAmABoxMigrationRunner"/> which re-detects state
/// under <c>pg_try_advisory_lock</c> and dispatches into fresh / bootstrap / normal paths
/// per ADR 0057 §3.
/// </summary>
public class PostgreSqlOutboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction> _detectionHelper;
    private readonly IAmABoxMigrationCatalog _catalog;
    private readonly IAmABoxPayloadModeValidator<NpgsqlConnection> _payloadValidator;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    /// <summary>
    /// Canonical ctor — Phase 8.2 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics.
    /// </summary>
    public PostgreSqlOutboxProvisioner(
        IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<NpgsqlConnection> payloadValidator,
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
    {
        _detectionHelper = detectionHelper;
        _catalog = catalog;
        _payloadValidator = payloadValidator;
        _configuration = configuration;
        _migrationRunner = migrationRunner;
    }

    /// <summary>
    /// Backward-compatible ctor preserving the spec 0027 public surface — used by existing
    /// call-sites (extensions + integration tests). Synthesises default singletons for the
    /// three role-interface dependencies; removed when the DI cascade lands in Phase 9.
    /// </summary>
    public PostgreSqlOutboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    public BoxType BoxType => BoxType.Outbox;
    public string BoxTableName => _configuration.OutBoxTableName;

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
            _configuration.OutBoxTableName,
            _configuration.SchemaName,
            BoxType.Outbox,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, _configuration.OutBoxTableName, _configuration.SchemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await _detectionHelper.DoesHistoryExistAsync(
            connection, _configuration.OutBoxTableName, _configuration.SchemaName, cancellationToken);
        if (!historyExists)
        {
            // Pre-lock detection is a hint for the caller; the runner re-detects under the lock.
            // Negative or zero return values are not gated here — the runner is the single source
            // of truth for discriminator violations and unknown-schema rejections.
            var detectedVersion = await _detectionHelper.DetectCurrentVersionAsync(
                connection, _configuration.OutBoxTableName, _configuration.SchemaName,
                BoxType.Outbox, migrations, cancellationToken);
            return new BoxTableState(
                TableExists: true, HistoryExists: false,
                CurrentVersion: detectedVersion < 0 ? 0 : detectedVersion);
        }

        var maxVersion = await _detectionHelper.GetMaxVersionAsync(
            connection, _configuration.OutBoxTableName, _configuration.SchemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await _payloadValidator.ValidateAsync(
            connection, _configuration.OutBoxTableName, _configuration.SchemaName,
            "body", _configuration.BinaryMessagePayload, cancellationToken);
    }
}
