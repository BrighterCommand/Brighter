using System;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Paramore.Brighter.Gcp.Tests.Helper;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Trait("Category", "Spanner")]
[Collection("Spanner")]
[Trait("Category", "Spanner")]
public class OutboxProvisionerFreshDatabaseTests : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerOutboxProvisioner _provisioner;

    public OutboxProvisionerFreshDatabaseTests()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
        _connectionString = Const.ConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);
        var runner = new SpannerBoxMigrationRunner(config);
        _provisioner = new SpannerOutboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            config,
            runner);
    }

    [Fact]
    public async Task Should_create_outbox_table()
    {
        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = CreateConnection();
        await connection.OpenAsync();

        Assert.Equal(1, await CountBoxTablesAsync(connection));
        Assert.Equal(1, await CountHistoryRowsAtLatestVersionAsync(connection));
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
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.OutboxLatest }
            });
        return (long)(await historyCheck.ExecuteScalarAsync())!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

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
