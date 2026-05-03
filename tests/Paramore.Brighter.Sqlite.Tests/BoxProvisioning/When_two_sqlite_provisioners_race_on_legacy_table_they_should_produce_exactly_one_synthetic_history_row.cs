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
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Sqlite.Tests.BoxProvisioning.Legacy;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class When_two_sqlite_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row : IAsyncLifetime
{
    private const int OutboxSeedVersion = 3;
    private const string OutboxMarkerMessageId = "outbox-marker-must-survive";
    private const string InboxMarkerCommandId = "inbox-marker-must-survive";

    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _outboxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _inboxTableName = $"test_inbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_produce_exactly_one_synthetic_v3_when_two_outbox_provisioners_race()
    {
        //Arrange — seed an outbox at V3 (no history row) plus a marker row to prove preservation.
        //Two independent provisioners (each with its own runner) race against the same table.
        //SQLite serializes via the writer slot — BEGIN IMMEDIATE in runner A acquires; runner B's
        //BEGIN IMMEDIATE returns SQLITE_BUSY and retries (per Task 4.4) until A commits.
        SqliteOutboxLegacySeeder.SeedAtV(OutboxSeedVersion, _connectionString, _outboxTableName);
        await SeedOutboxMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _outboxTableName);
        var provisionerA = new SqliteOutboxProvisioner(config, new SqliteBoxMigrationRunner(config));
        var provisionerB = new SqliteOutboxProvisioner(config, new SqliteBoxMigrationRunner(config));

        //Act — race two provisioners against the same legacy table.
        await Task.WhenAll(provisionerA.ProvisionAsync(), provisionerB.ProvisionAsync());

        //Assert — history has exactly one synthetic V3 + one applied per V4..V7 (no duplicates).
        //Whichever runner acquires the writer slot first enters the bootstrap path and stamps
        //V3..V_latest; the loser, on its second attempt, sees historyExistsNow=true under the
        //lock-bearing transaction and enters the normal path (no-op since MAX(V)=V_latest).
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
        Assert.Contains("DataRef", await GetTableColumns(_outboxTableName));
        Assert.Equal(1, await GetOutboxMarkerRowCount());
    }

    [Fact]
    public async Task Should_produce_exactly_one_synthetic_v1_when_two_inbox_provisioners_race()
    {
        //Arrange — seed a V1 inbox (no ContextKey, no history) plus a marker command row.
        SqliteInboxLegacySeeder.SeedAtV1(_connectionString, _inboxTableName);
        await SeedInboxMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: _inboxTableName);
        var provisionerA = new SqliteInboxProvisioner(config, new SqliteBoxMigrationRunner(config));
        var provisionerB = new SqliteInboxProvisioner(config, new SqliteBoxMigrationRunner(config));

        //Act — race two provisioners against the same legacy table.
        await Task.WhenAll(provisionerA.ProvisionAsync(), provisionerB.ProvisionAsync());

        //Assert — exactly one synthetic V1 + one applied V2 (no duplicates).
        var rowsByVersion = await GetHistoryRowsByVersion(_inboxTableName);
        Assert.Equal(ExpectedMigrationVersions.InboxLatest, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(1, rowsByVersion);
        Assert.StartsWith("bootstrap: detected at V1", syntheticDescription);

        var appliedDescription = Assert.Contains(2, rowsByVersion);
        Assert.False(
            appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
            $"V2 should be an applied migration row, not a synthetic bootstrap row " +
            $"(description was: '{appliedDescription}')");

        //Assert — table ends at V2 with seeded marker preserved (ContextKey NULL on existing row).
        Assert.Contains("ContextKey", await GetTableColumns(_inboxTableName));
        Assert.True(await InboxMarkerRowExistsWithNullContextKey());
    }

    private async Task SeedOutboxMarkerRow()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO [{_outboxTableName}] ([MessageId]) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", OutboxMarkerMessageId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedInboxMarkerRow()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO [{_inboxTableName}] ([CommandId]) VALUES (@CommandId)";
        command.Parameters.AddWithValue("@CommandId", InboxMarkerCommandId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetTableColumns(string tableName)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info(@TableName)";
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private async Task<Dictionary<int, string>> GetHistoryRowsByVersion(string tableName)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT [MigrationVersion], [Description] FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName
ORDER BY [MigrationVersion]";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        var rows = new Dictionary<int, string>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows[reader.GetInt32(0)] = reader.GetString(1);
            }
        }
        catch (SqliteException)
        {
            // history table absent — no rows to return
        }
        return rows;
    }

    private async Task<long> GetOutboxMarkerRowCount()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{_outboxTableName}] WHERE [MessageId] = @MessageId";
        command.Parameters.AddWithValue("@MessageId", OutboxMarkerMessageId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task<bool> InboxMarkerRowExistsWithNullContextKey()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM [{_inboxTableName}]
WHERE [CommandId] = @CommandId AND [ContextKey] IS NULL";
        command.Parameters.AddWithValue("@CommandId", InboxMarkerCommandId);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1L;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var dropOutbox = connection.CreateCommand();
            dropOutbox.CommandText = $"DROP TABLE IF EXISTS [{_outboxTableName}]";
            await dropOutbox.ExecuteNonQueryAsync();

            await using var dropInbox = connection.CreateCommand();
            dropInbox.CommandText = $"DROP TABLE IF EXISTS [{_inboxTableName}]";
            await dropInbox.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText =
                "DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] IN (@OutboxTable, @InboxTable)";
            deleteHistory.Parameters.AddWithValue("@OutboxTable", _outboxTableName);
            deleteHistory.Parameters.AddWithValue("@InboxTable", _inboxTableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (SqliteException) { /* history table may not exist */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
