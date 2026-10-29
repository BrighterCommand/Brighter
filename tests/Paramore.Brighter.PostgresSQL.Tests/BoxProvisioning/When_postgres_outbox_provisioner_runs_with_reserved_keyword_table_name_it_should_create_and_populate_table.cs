using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// Postgres identifiers are lowercased, then quoted, so a reserved-keyword table name (<c>Order</c>) provisions, replays
/// idempotently, round-trips a message, and keys its history under the folded name <c>order</c>.
/// </summary>
public class PostgreSqlReservedKeywordTableNameTests
    : IAsyncLifetime
{
    private const string ReservedKeywordTableName = "Order";
    private const string ExpectedPhysicalTableName = "order";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly RelationalDatabaseConfiguration _config;
    private readonly PostgreSqlOutboxProvisioner _provisioner;
    private readonly PostgreSqlBoxMigrationRunner _runner;

    public PostgreSqlReservedKeywordTableNameTests()
    {
        _config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: ReservedKeywordTableName);
        _runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), _config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            _config,
            _runner);
    }

    [Fact]
    public async Task When_postgres_outbox_provisioner_runs_with_reserved_keyword_table_name_it_should_create_and_populate_table()
    {
        new PostgresSqlTestHelper().SetupDatabase();

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using var tableCheck = connection.CreateCommand();
            tableCheck.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'public' AND TABLE_NAME = @TableName)";
            tableCheck.Parameters.AddWithValue("@TableName", ExpectedPhysicalTableName);
            var tableExists = (bool)(await tableCheck.ExecuteScalarAsync())!;
            Assert.True(tableExists, "Reserved-keyword table should exist under PG-folded lowercase name");

            using var historyCheck = connection.CreateCommand();
            historyCheck.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'";
            historyCheck.Parameters.AddWithValue("@BoxTableName", ExpectedPhysicalTableName);
            var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
            Assert.Equal(1, historyCount);
        }

        // Act
        await _provisioner.ProvisionAsync();

        // Assert
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var historyCheck = connection.CreateCommand();
            historyCheck.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'";
            historyCheck.Parameters.AddWithValue("@BoxTableName", ExpectedPhysicalTableName);
            var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
            Assert.Equal(1, historyCount);
        }

        // Act + Assert
        var outbox = new PostgreSqlOutbox(_config);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test.topic"), MessageType.MT_COMMAND),
            new MessageBody("hello reserved keyword"));

        await outbox.AddAsync(message, new RequestContext());
        var roundTripped = await outbox.GetAsync(message.Id, new RequestContext());

        Assert.Equal(message.Id, roundTripped.Id);
        Assert.Equal(message.Body.Value, roundTripped.Body.Value);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $@"
DROP TABLE IF EXISTS ""{ExpectedPhysicalTableName}"";
DELETE FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = '{ExpectedPhysicalTableName}' AND ""SchemaName"" = 'public';";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
