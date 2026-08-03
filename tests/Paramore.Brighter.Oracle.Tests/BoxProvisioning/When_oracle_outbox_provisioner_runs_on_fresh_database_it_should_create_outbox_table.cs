using System;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning.Oracle;
using Xunit;

namespace Paramore.Brighter.Oracle.Tests.BoxProvisioning;

public class OracleOutboxProvisionerFreshDatabaseTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName;
    private readonly OracleOutboxProvisioner _provisioner;

    public OracleOutboxProvisionerFreshDatabaseTests()
    {
        _tableName = $"TEST_OUTBOX_{Guid.NewGuid():N}";

        var configuration = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName);

        var runner = new OracleBoxMigrationRunner(
            new OracleOutboxMigrationCatalog(),
            configuration,
            TimeSpan.FromSeconds(30));

        _provisioner = new OracleOutboxProvisioner(
            new OracleBoxDetectionHelper(),
            new OracleOutboxMigrationCatalog(),
            new OraclePayloadModeValidator(),
            configuration,
            runner);
    }

    [Fact]
    public async Task When_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table()
    {
        await _provisioner.ProvisionAsync();

        var tableExists = await OracleBoxProvisioningTestHelper.TableExistsAsync(_connectionString, _tableName);
        Assert.True(tableExists);

        var historyCount = await OracleBoxProvisioningTestHelper.HistoryCountForVersionAsync(
            _connectionString,
            _tableName,
            ExpectedMigrationVersions.OutboxLatest);

        Assert.Equal(1, historyCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await OracleBoxProvisioningTestHelper.DropTableIfExistsAsync(_connectionString, _tableName);
    }
}
