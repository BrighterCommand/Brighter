using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlTrippedTopicsProvisionerTests : IAsyncLifetime
{
    private const int TRIP_TABLE_VERSION = 1;
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_trips_{Guid.NewGuid():N}";
    private readonly string _nonDefaultSchema = $"billing_for_trip_test_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_postgresql_tripped_topics_provisioner_runs_on_fresh_database_it_should_create_the_trip_table()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();
        var provisioner = ProvisionerFor(schemaName: null);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.Equal(BoxType.TrippedTopics, provisioner.BoxType);
        Assert.Equal(_tableName, provisioner.BoxTableName.Value);
        Assert.True(await TableExistsInSchemaAsync("public"));
        Assert.Equal(["ended_at", "prior_trip_time", "topic", "trip_time"],
            (await ColumnsOfAsync("public")).OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(1, await HistoryRowCountAsync("public"));
    }

    [Fact]
    public async Task When_postgresql_tripped_topics_provisioner_runs_twice_it_should_be_idempotent()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();
        var provisioner = ProvisionerFor(schemaName: null);
        await provisioner.ProvisionAsync();

        //Act
        var exception = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Null(exception);
        Assert.True(await TableExistsInSchemaAsync("public"));
        Assert.Equal(1, await HistoryRowCountAsync("public"));
    }

    [Fact]
    public async Task When_postgresql_tripped_topics_provisioner_finds_a_hand_created_table_it_should_bootstrap()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();
        await ExecuteNonQueryAsync(PostgreSqlTrippedTopicStore.GetDDL(_tableName));
        var provisioner = ProvisionerFor(schemaName: null);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.True(await TableExistsInSchemaAsync("public"));
        Assert.Equal(1, await HistoryRowCountAsync("public"));
    }

    [Fact]
    public async Task When_postgresql_tripped_topics_provisioner_runs_with_a_non_default_schema_it_should_create_in_that_schema()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();
        await ExecuteNonQueryAsync($@"CREATE SCHEMA IF NOT EXISTS ""{_nonDefaultSchema}""");
        var provisioner = ProvisionerFor(_nonDefaultSchema);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.True(await TableExistsInSchemaAsync(_nonDefaultSchema));
        Assert.False(await TableExistsInSchemaAsync("public"));
        Assert.Equal(1, await HistoryRowCountAsync(_nonDefaultSchema));
    }

    private IAmABoxProvisioner ProvisionerFor(string? schemaName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(_connectionString, schemaName: schemaName);

        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddPostgreSqlTrippedTopics(configuration, _tableName, schemaName));

        return services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single();
    }

    private async Task<bool> TableExistsInSchemaAsync(string schemaName) => (bool)(await ScalarAsync(
        $"SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schemaName}' AND TABLE_NAME = '{_tableName}')"))!;

    private async Task<long> HistoryRowCountAsync(string schemaName) => (long)(await ScalarAsync($@"
SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = '{_tableName}' AND ""SchemaName"" = '{schemaName}' AND ""MigrationVersion"" = {TRIP_TABLE_VERSION}"))!;

    private async Task<List<string>> ColumnsOfAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT column_name FROM information_schema.columns WHERE table_schema = '{schemaName}' AND table_name = '{_tableName}'";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        return columns;
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await ExecuteNonQueryAsync($@"DROP TABLE IF EXISTS ""public"".""{_tableName}""");
            await ExecuteNonQueryAsync($@"DROP SCHEMA IF EXISTS ""{_nonDefaultSchema}"" CASCADE");
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
