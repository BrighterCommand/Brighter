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
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.Legacy;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_two_postgres_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row : IAsyncLifetime
{
    private const int OutboxSeedVersion = 3;
    private const string OutboxMarkerMessageId = "outbox-marker-must-survive";
    private const string InboxMarkerCommandId = "inbox-marker-must-survive";
    private const string InboxMarkerContextKey = "marker-context";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _outboxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _inboxTableName = $"test_inbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_produce_exactly_one_synthetic_v3_when_two_outbox_provisioners_race()
    {
        //Arrange — seed an outbox at V3 (no history row) plus a marker row to prove preservation.
        new PostgresSqlTestHelper().SetupDatabase();
        PostgreSqlOutboxLegacySeeder.SeedAtV(OutboxSeedVersion, _connectionString, _outboxTableName);
        await SeedOutboxMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _outboxTableName);
        var provisionerA = new PostgreSqlOutboxProvisioner(config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));
        var provisionerB = new PostgreSqlOutboxProvisioner(config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));

        //Act — race two provisioners against the same legacy table.
        await Task.WhenAll(provisionerA.ProvisionAsync(), provisionerB.ProvisionAsync());

        //Assert — history has exactly one synthetic V3 + one applied per V4..V7 (no duplicates).
        var rowsByVersion = await GetHistoryRowsByVersion(_outboxTableName);
        Assert.Equal(ExpectedMigrationVersions.OutboxLatest - OutboxSeedVersion + 1, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(OutboxSeedVersion, rowsByVersion);
        Assert.StartsWith($"bootstrap: detected at V{OutboxSeedVersion}", syntheticDescription);

        for (var v = OutboxSeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var appliedDescription = Assert.Contains(v, rowsByVersion);
            Assert.False(
                appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be an applied migration row, not a synthetic bootstrap row " +
                $"(description was: '{appliedDescription}')");
        }

        //Assert — table ends at V7 with seeded marker preserved.
        Assert.Contains("dataref", await GetTableColumns(_outboxTableName));
        Assert.Equal(1, await GetOutboxMarkerRowCount());
    }

    [Fact]
    public async Task Should_produce_exactly_one_synthetic_v1_when_two_inbox_provisioners_race()
    {
        //Arrange — seed a V1 Postgres inbox (V1-only chain) plus a marker command row.
        new PostgresSqlTestHelper().SetupDatabase();
        PostgreSqlInboxLegacySeeder.SeedAtV1(_connectionString, _inboxTableName);
        await SeedInboxMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: _inboxTableName);
        var provisionerA = new PostgreSqlInboxProvisioner(config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));
        var provisionerB = new PostgreSqlInboxProvisioner(config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));

        //Act — race two provisioners against the same legacy table.
        await Task.WhenAll(provisionerA.ProvisionAsync(), provisionerB.ProvisionAsync());

        //Assert — exactly one synthetic V1 row (Postgres inbox is V1-only — no V2 to apply).
        var rowsByVersion = await GetHistoryRowsByVersion(_inboxTableName);
        Assert.Single(rowsByVersion);

        var syntheticDescription = Assert.Contains(1, rowsByVersion);
        Assert.StartsWith("bootstrap: detected at V1", syntheticDescription);

        //Assert — V1 columns intact (commandid, contextkey both present in composite PK) and marker preserved.
        var columns = await GetTableColumns(_inboxTableName);
        Assert.Contains("commandid", columns);
        Assert.Contains("contextkey", columns);
        Assert.True(await InboxMarkerRowExists());
    }

    private async Task SeedOutboxMarkerRow()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO \"{_outboxTableName}\" (messageid) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", OutboxMarkerMessageId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedInboxMarkerRow()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO \"{_inboxTableName}\" (commandid, contextkey) VALUES (@CommandId, @ContextKey)";
        command.Parameters.AddWithValue("@CommandId", InboxMarkerCommandId);
        command.Parameters.AddWithValue("@ContextKey", InboxMarkerContextKey);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetTableColumns(string tableName)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT column_name FROM information_schema.columns
WHERE table_name = @TableName AND table_schema = 'public'";
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private async Task<Dictionary<int, string>> GetHistoryRowsByVersion(string tableName)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT ""MigrationVersion"", ""Description"" FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'
ORDER BY ""MigrationVersion""";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        var rows = new Dictionary<int, string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    private async Task<long> GetOutboxMarkerRowCount()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM \"{_outboxTableName}\" WHERE messageid = @MessageId";
        command.Parameters.AddWithValue("@MessageId", OutboxMarkerMessageId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> InboxMarkerRowExists()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{_inboxTableName}""
WHERE commandid = @CommandId AND contextkey = @ContextKey";
        command.Parameters.AddWithValue("@CommandId", InboxMarkerCommandId);
        command.Parameters.AddWithValue("@ContextKey", InboxMarkerContextKey);
        return (long)(await command.ExecuteScalarAsync())! == 1;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var dropOutbox = connection.CreateCommand();
            dropOutbox.CommandText = $"DROP TABLE IF EXISTS \"{_outboxTableName}\"";
            await dropOutbox.ExecuteNonQueryAsync();

            using var dropInbox = connection.CreateCommand();
            dropInbox.CommandText = $"DROP TABLE IF EXISTS \"{_inboxTableName}\"";
            await dropInbox.ExecuteNonQueryAsync();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DELETE FROM ""__BrighterMigrationHistory"" WHERE ""BoxTableName"" IN (@OutboxTable, @InboxTable)";
            deleteHistory.Parameters.AddWithValue("@OutboxTable", _outboxTableName);
            deleteHistory.Parameters.AddWithValue("@InboxTable", _inboxTableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (PostgresException) { /* history table may not exist if runner rolled back */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
