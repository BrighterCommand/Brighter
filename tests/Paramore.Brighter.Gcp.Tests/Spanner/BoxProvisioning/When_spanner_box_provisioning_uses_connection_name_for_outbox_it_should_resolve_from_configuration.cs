using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Property("Category", "Spanner")]
[NotInParallel]
[Property("Category", "Spanner")]
public class When_spanner_box_provisioning_uses_connection_name_for_outbox_it_should_resolve_from_configuration
{
    private readonly string _connectionString = Const.ConnectionString;
    private readonly string _tableName;

    public When_spanner_box_provisioning_uses_connection_name_for_outbox_it_should_resolve_from_configuration()
    {
        _tableName = $"test_outbox_{Guid.NewGuid():N}";
    }

    [Test]
    public async Task Should_resolve_connection_string_and_provision()
    {
        //Arrange
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:BrighterDb"] = _connectionString
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        var stubBuilder = new StubBrighterBuilder(services);
        stubBuilder.UseBoxProvisioning(opts =>
        {
            opts.AddSpannerOutbox("BrighterDb", outboxTableName: _tableName);
        });

        var provider = services.BuildServiceProvider();
        var provisioners = provider.GetServices<IAmABoxProvisioner>().ToList();

        //Act
        await Assert.That(provisioners).HasSingleItem();
        await provisioners[0].ProvisionAsync();

        //Assert
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var tableCheck = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, _tableName } });
        var tableCount = (long)(await tableCheck.ExecuteScalarAsync())!;
        await Assert.That(tableCount).IsEqualTo(1);
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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