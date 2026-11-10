using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_the_tripped_topics_provisioner_runs_it_should_invoke_the_migration_runner_for_the_trip_table
{
    [Fact]
    public async Task Should_call_the_runner_once_with_the_trip_table_the_schema_and_the_tripped_topics_box_type()
    {
        //Arrange
        var runner = new SpyBoxMigrationRunner();
        var provisioner = new TrippedTopicsProvisioner("BrighterTrippedTopics", new SchemaName("brighter"), runner);

        //Act
        await provisioner.ProvisionAsync(CancellationToken.None);

        //Assert
        Assert.Equal(BoxType.TrippedTopics, provisioner.BoxType);
        Assert.Equal("BrighterTrippedTopics", provisioner.BoxTableName.Value);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("BrighterTrippedTopics", call.TableName.Value);
        Assert.Equal("brighter", call.SchemaName?.Value);
        Assert.Equal(BoxType.TrippedTopics, call.BoxType);
    }

    [Fact]
    public async Task Should_call_the_runner_with_no_schema_when_none_is_configured()
    {
        //Arrange
        var runner = new SpyBoxMigrationRunner();
        var provisioner = new TrippedTopicsProvisioner("BrighterTrippedTopics", null, runner);

        //Act
        await provisioner.ProvisionAsync(CancellationToken.None);

        //Assert
        var call = Assert.Single(runner.Calls);
        Assert.Null(call.SchemaName);
    }

    [Theory]
    [InlineData("Trips; DROP TABLE Outbox", null)]
    [InlineData("1Trips", null)]
    [InlineData("BrighterTrippedTopics", "dbo; DROP TABLE Outbox")]
    public async Task Should_reject_an_unsafe_table_or_schema_name_before_calling_the_runner(string tableName, string? schemaName)
    {
        //Arrange
        var runner = new SpyBoxMigrationRunner();
        var provisioner = new TrippedTopicsProvisioner(
            tableName, schemaName is null ? null : new SchemaName(schemaName), runner);

        //Act
        await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync(CancellationToken.None));

        //Assert
        Assert.Empty(runner.Calls);
    }
}
