using System;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning.Oracle;
using Xunit;

namespace Paramore.Brighter.Oracle.Tests.BoxProvisioning;

public class OracleInboxProvisionerFreshDatabaseTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName;
    private readonly OracleInboxProvisioner _provisioner;

    public OracleInboxProvisionerFreshDatabaseTests()
    {
        _tableName = $"TEST_INBOX_{Guid.NewGuid():N}";

        var configuration = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName);

        var runner = new OracleBoxMigrationRunner(
            new OracleInboxMigrationCatalog(),
            configuration,
            TimeSpan.FromSeconds(30));

        _provisioner = new OracleInboxProvisioner(
            new OracleBoxDetectionHelper(),
            new OracleInboxMigrationCatalog(),
            new OraclePayloadModeValidator(),
            configuration,
            runner);
    }

    [Fact]
    public async Task When_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table()
    {
        await _provisioner.ProvisionAsync();

        var tableExists = await OracleBoxProvisioningTestHelper.TableExistsAsync(_connectionString, _tableName);
        Assert.True(tableExists);

        var historyCount = await OracleBoxProvisioningTestHelper.HistoryCountForVersionAsync(
            _connectionString,
            _tableName,
            ExpectedMigrationVersions.InboxLatest);

        Assert.Equal(1, historyCount);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await OracleBoxProvisioningTestHelper.DropTableIfExistsAsync(_connectionString, _tableName);
    }
}
