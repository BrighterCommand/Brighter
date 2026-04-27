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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Provisions a MSSQL inbox table. Performs a pre-lock detection pass to gate payload-mode
/// validation, then delegates to <see cref="IAmABoxMigrationRunner"/> which re-detects state
/// under the lock and dispatches into fresh / bootstrap / normal paths per ADR 0057 §3.
/// </summary>
public class MsSqlInboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    public BoxType BoxType => BoxType.Inbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MsSqlInboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(migrations, cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.InBoxTableName,
            configuration.SchemaName,
            BoxType.Inbox,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        System.Collections.Generic.IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await MsSqlBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await MsSqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        if (!historyExists)
        {
            var detectedVersion = await MsSqlBoxDetectionHelpers.DetectCurrentVersionAsync(
                connection, configuration.InBoxTableName, schemaName,
                BoxType.Inbox, migrations, cancellationToken);
            return new BoxTableState(
                TableExists: true, HistoryExists: false,
                CurrentVersion: detectedVersion < 0 ? 0 : detectedVersion);
        }

        var maxVersion = await MsSqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.InBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? "dbo";

        using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MsSqlPayloadModeValidator.ValidateAsync(
            connection, configuration.InBoxTableName, schemaName,
            "CommandBody", configuration.BinaryMessagePayload, cancellationToken);
    }
}
