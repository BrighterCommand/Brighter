using System;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Outbox.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Collection("SpannerBoxProvisioning")]
public class When_spanner_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap : IAsyncLifetime
{
    private readonly string _tableName;
    private readonly string _connectionString;
    private readonly SpannerOutboxProvisioner _provisioner;

    public When_spanner_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()
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
    public async Task Should_bootstrap_with_synthetic_history()
    {
        // Arrange — create outbox table directly (simulating pre-migration install)
        using (var setupConn = CreateConnection())
        {
            await setupConn.OpenAsync();
            var ddl = setupConn.CreateDdlCommand(SpannerOutboxBuilder.GetDDL(_tableName));
            await ddl.ExecuteNonQueryAsync();
        }

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var historyCheck = connection.CreateSelectCommand(
            @"SELECT COUNT(1) FROM `BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `MigrationVersion` = @ExpectedVersion",
            new SpannerParameterCollection
            {
                { "BoxTableName", SpannerDbType.String, _tableName },
                { "ExpectedVersion", SpannerDbType.Int64, (long)ExpectedMigrationVersions.OutboxLatest }
            });
        var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
        Assert.Equal(1, historyCount);
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
