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
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Provisions a MySQL inbox table. Performs a pre-lock detection pass to gate payload-mode
/// validation, then delegates to <see cref="IAmABoxMigrationRunner"/> which re-detects state
/// under <c>GET_LOCK</c> and dispatches into fresh / bootstrap / normal paths per ADR 0057 §3.
/// </summary>
public class MySqlInboxProvisioner : IAmABoxProvisioner
{
    private readonly IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction> _detectionHelper;
    private readonly IAmABoxMigrationCatalog _catalog;
    private readonly IAmABoxPayloadModeValidator<MySqlConnection> _payloadValidator;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    /// <summary>
    /// Canonical ctor — Phase 8.3 of spec 0028. Takes the role-interface dependencies
    /// explicitly so the provisioner does not reach for backend statics.
    /// </summary>
    public MySqlInboxProvisioner(
        IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction> detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmABoxPayloadModeValidator<MySqlConnection> payloadValidator,
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
    public MySqlInboxProvisioner(
        IAmARelationalDatabaseConfiguration configuration,
        IAmABoxMigrationRunner migrationRunner)
        : this(
            new MySqlBoxDetectionHelper(),
            new MySqlInboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            configuration,
            migrationRunner)
    {
    }

    public BoxType BoxType => BoxType.Inbox;
    public string BoxTableName => _configuration.InBoxTableName;

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
            _configuration.InBoxTableName,
            _configuration.SchemaName,
            BoxType.Inbox,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, _configuration.InBoxTableName, _configuration.SchemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await _detectionHelper.DoesHistoryExistAsync(
            connection, _configuration.InBoxTableName, _configuration.SchemaName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await _detectionHelper.DetectCurrentVersionAsync(
                connection, _configuration.InBoxTableName, _configuration.SchemaName,
                BoxType.Inbox, migrations, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await _detectionHelper.GetMaxVersionAsync(
            connection, _configuration.InBoxTableName, _configuration.SchemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        using var connection = new MySqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await _payloadValidator.ValidateAsync(
            connection, _configuration.InBoxTableName, _configuration.SchemaName,
            "CommandBody", _configuration.BinaryMessagePayload, cancellationToken);
    }
}
