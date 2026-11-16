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

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlTrippedTopicsProvisionerTests : IAsyncLifetime
{
    private const string NonDefaultSchema = "billing_for_trip_test";
    private const int TRIP_TABLE_VERSION = 1;
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_trips_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_mssql_tripped_topics_provisioner_runs_on_fresh_database_it_should_create_the_trip_table()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        var provisioner = ProvisionerFor(schemaName: null);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.Equal(BoxType.TrippedTopics, provisioner.BoxType);
        Assert.Equal(_tableName, provisioner.BoxTableName.Value);
        Assert.True(TableExistsInSchema("dbo"));
        Assert.Equal(["EndedAt", "PriorTripTime", "Topic", "TripTime"], ColumnsOf("dbo").OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(1, HistoryRowCount("dbo"));
    }

    [Fact]
    public async Task When_mssql_tripped_topics_provisioner_runs_twice_it_should_be_idempotent()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        var provisioner = ProvisionerFor(schemaName: null);
        await provisioner.ProvisionAsync();

        //Act
        var exception = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Null(exception);
        Assert.True(TableExistsInSchema("dbo"));
        Assert.Equal(1, HistoryRowCount("dbo"));
    }

    [Fact]
    public async Task When_mssql_tripped_topics_provisioner_finds_a_hand_created_table_it_should_bootstrap()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        Configuration.CreateTable(_connectionString, MsSqlTrippedTopicStore.GetDDL(_tableName));
        var provisioner = ProvisionerFor(schemaName: null);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.True(TableExistsInSchema("dbo"));
        Assert.Equal(1, HistoryRowCount("dbo"));
    }

    [Fact]
    public async Task When_mssql_tripped_topics_provisioner_runs_with_a_non_default_schema_it_should_create_in_that_schema()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        ExecuteNonQuery($"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{NonDefaultSchema}') EXEC('CREATE SCHEMA [{NonDefaultSchema}]')");
        var provisioner = ProvisionerFor(NonDefaultSchema);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.True(TableExistsInSchema(NonDefaultSchema));
        Assert.False(TableExistsInSchema("dbo"));
        Assert.Equal(1, HistoryRowCount(NonDefaultSchema));
    }

    private IAmABoxProvisioner ProvisionerFor(string? schemaName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(_connectionString, schemaName: schemaName);

        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddMsSqlTrippedTopics(configuration, _tableName, schemaName));

        return services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single();
    }

    private bool TableExistsInSchema(string schemaName) => ScalarInt($@"
SELECT COUNT(1) FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = '{_tableName}' AND s.name = '{schemaName}'") == 1;

    private string[] ColumnsOf(string schemaName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{_tableName}' AND TABLE_SCHEMA = '{schemaName}'";
        using var reader = command.ExecuteReader();
        var columns = new System.Collections.Generic.List<string>();
        while (reader.Read()) columns.Add(reader.GetString(0));
        return columns.ToArray();
    }

    private int HistoryRowCount(string schemaName) => ScalarInt($@"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = '{_tableName}' AND [SchemaName] = '{schemaName}' AND [MigrationVersion] = {TRIP_TABLE_VERSION}");

    private int ScalarInt(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (int)command.ExecuteScalar()!;
    }

    private void ExecuteNonQuery(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try
        {
            ExecuteNonQuery($"DROP TABLE IF EXISTS [dbo].[{_tableName}]");
            ExecuteNonQuery($"IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{NonDefaultSchema}') DROP TABLE IF EXISTS [{NonDefaultSchema}].[{_tableName}]");
        }
        catch
        {
            // Best effort cleanup
        }

        return Task.CompletedTask;
    }
}
