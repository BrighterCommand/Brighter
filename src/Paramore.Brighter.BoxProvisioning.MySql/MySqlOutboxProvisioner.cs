using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Provisions a MySQL outbox table.
/// </summary>
public class MySqlOutboxProvisioner(
    IAmARelationalDatabaseConfiguration configuration,
    IAmABoxMigrationRunner migrationRunner) : IAmABoxProvisioner
{
    public BoxType BoxType => BoxType.Outbox;

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var migrations = MySqlOutboxMigrations.All(configuration);
        var tableState = await DetectTableStateAsync(migrations, cancellationToken);

        if (tableState.TableExists)
        {
            await ValidatePayloadModeAsync(cancellationToken);
        }

        await migrationRunner.MigrateAsync(
            configuration.OutBoxTableName,
            configuration.SchemaName,
            BoxType.Outbox,
            migrations,
            tableState,
            cancellationToken);
    }

    private async Task<BoxTableState> DetectTableStateAsync(
        System.Collections.Generic.IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableExists = await MySqlBoxDetectionHelpers.DoesTableExistAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        if (!tableExists)
            return new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var historyExists = await MySqlBoxDetectionHelpers.DoesHistoryExistAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);

        if (!historyExists)
        {
            var detectedVersion = await MySqlBoxDetectionHelpers.DetectCurrentVersionAsync(
                connection, configuration.OutBoxTableName, schemaName,
                BoxType.Outbox, migrations, cancellationToken);
            return new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: detectedVersion);
        }

        var maxVersion = await MySqlBoxDetectionHelpers.GetMaxVersionAsync(
            connection, configuration.OutBoxTableName, schemaName, cancellationToken);
        return new BoxTableState(TableExists: true, HistoryExists: true, CurrentVersion: maxVersion);
    }

    private async Task ValidatePayloadModeAsync(CancellationToken cancellationToken)
    {
        var schemaName = configuration.SchemaName ?? DatabaseName();

        using var connection = new MySqlConnection(configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await MySqlPayloadModeValidator.ValidateAsync(
            connection, configuration.OutBoxTableName, schemaName,
            "Body", configuration.BinaryMessagePayload, cancellationToken);
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(configuration.ConnectionString);
        return builder.Database;
    }
}
