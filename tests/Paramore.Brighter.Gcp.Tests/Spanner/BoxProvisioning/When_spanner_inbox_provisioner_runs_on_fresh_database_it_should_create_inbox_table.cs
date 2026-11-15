using System;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Paramore.Brighter.Gcp.Tests.Helper;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Property("Category", "Spanner")]
[NotInParallel]
[Property("Category", "Spanner")]
public class InboxProvisionerFreshDatabaseTests
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerInboxProvisioner _provisioner;

    public InboxProvisionerFreshDatabaseTests()
    {
        _tableName = $"test_inbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName);
        var runner = new SpannerBoxMigrationRunner(config);
        _provisioner = new SpannerInboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task Should_create_inbox_table()
    {
        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = CreateConnection();
        await connection.OpenAsync();

        await Assert.That(await CountBoxTablesAsync(connection)).IsEqualTo(1);
        await Assert.That(await CountHistoryRowsAtLatestVersionAsync(connection)).IsEqualTo(1);
    }

    private async Task<long> CountBoxTablesAsync(SpannerConnection connection)
    {
        using var tableCheck = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, _tableName } });
        return (long)(await tableCheck.ExecuteScalarAsync())!;
    }

    private async Task<long> CountHistoryRowsAtLatestVersionAsync(SpannerConnection connection)
    {
        using var historyCheck = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, _tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.InboxLatest }
            });
        return (long)(await historyCheck.ExecuteScalarAsync())!;
    }

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            var dropTable = connection.CreateDdlCommand($"DROP TABLE IF EXISTS `{_tableName}`");
            await dropTable.ExecuteNonQueryAsync();
        }
        catch { }
    }

    private SpannerConnection CreateConnection()
    {
        var builder = new SpannerConnectionStringBuilder(_connectionString)
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        };
        return new SpannerConnection(builder);
    }
}
