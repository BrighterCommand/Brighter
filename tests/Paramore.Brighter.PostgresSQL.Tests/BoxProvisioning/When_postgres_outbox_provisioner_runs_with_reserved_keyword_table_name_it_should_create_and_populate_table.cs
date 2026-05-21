using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// Pins the lowercase-then-quote contract for PG identifiers. Configures the outbox with a
/// table name that is a PG reserved keyword (<c>Order</c>) — which the legacy unquoted DDL
/// path would reject at parse time — and asserts that provisioning succeeds, chain replay
/// is idempotent, runtime DML round-trips a message, and history is keyed under the
/// PG-folded lowercase form.
/// </summary>
public class When_postgres_outbox_provisioner_runs_with_reserved_keyword_table_name_it_should_create_and_populate_table
    : IAsyncLifetime
{
    // "Order" is a SQL reserved keyword. Unquoted PG DDL `CREATE TABLE Order ...` is a
    // syntax error. The lowercase-then-quote convention emits `CREATE TABLE "order" ...`,
    // which is legal, and resolves to the same physical table the legacy unquoted form
    // would have created for a configured `"Outbox"` (folded to `outbox`).
    private const string ReservedKeywordTableName = "Order";
    private const string ExpectedPhysicalTableName = "order";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly RelationalDatabaseConfiguration _config;
    private readonly PostgreSqlOutboxProvisioner _provisioner;
    private readonly PostgreSqlBoxMigrationRunner _runner;

    public When_postgres_outbox_provisioner_runs_with_reserved_keyword_table_name_it_should_create_and_populate_table()
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
    public async Task Should_provision_reserved_keyword_table_and_round_trip_a_message()
    {
        new PostgresSqlTestHelper().SetupDatabase();

        // Act: fresh install
        await _provisioner.ProvisionAsync();

        // Assert: physical table exists under PG-folded lowercase name
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

            // History row keyed under lowercase BoxTableName so detection on the next run
            // matches the same row.
            using var historyCheck = connection.CreateCommand();
            historyCheck.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'";
            historyCheck.Parameters.AddWithValue("@BoxTableName", ExpectedPhysicalTableName);
            var historyCount = (long)(await historyCheck.ExecuteScalarAsync())!;
            Assert.Equal(1, historyCount);
        }

        // Act: chain replay — second provision should be a no-op (history row already
        // matches latest version, no DDL re-executed).
        await _provisioner.ProvisionAsync();

        // Assert: still exactly one history row at V_latest (no duplicate, no further DDL).
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

        // Act + Assert: runtime DML — write a message via the PG outbox and read it back.
        // Exercises GenerateSqlText override that lowercases-quotes the table name; with the
        // legacy unquoted form this would emit `INSERT INTO Order ...` and fail at parse.
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
            // Drop both the physical (folded) table and its history rows.
            command.CommandText = $@"
DROP TABLE IF EXISTS ""{ExpectedPhysicalTableName}"";
DELETE FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = '{ExpectedPhysicalTableName}' AND ""SchemaName"" = 'public';";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
