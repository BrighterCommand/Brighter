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
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Sqlite.Tests.BoxProvisioning.Legacy;
using Paramore.Brighter.Sqlite.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class When_sqlite_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically : IAsyncLifetime
{
    private const int SeedVersion = 3;
    private const int BrokenVersion = 6;

    //SQLite has no RAISE-EXCEPTION-from-DML form (RAISE is only valid inside triggers), so we
    //force a SQL error by referencing a non-existent table — SqliteException with code 1
    //(SQLITE_ERROR), which is propagated by the runner without retry (only SQLITE_BUSY=5 retries).
    private const string BrokenUpScript =
        "SELECT 1 FROM [__forced_failure_for_spec_0027_task_4_8a];";

    private const string MarkerMessageId = "marker-row-must-survive";

    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_roll_back_all_history_and_ddl_then_succeed_on_retry()
    {
        //Arrange — seed an outbox at V3 (no history) plus a marker row to prove preservation
        //across the rollback.
        SqliteOutboxLegacySeeder.SeedAtV(SeedVersion, _connectionString, _tableName);
        await SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realMigrations = new SqliteOutboxMigrationCatalog().All(config);
        var brokenMigrations = BrokenMigrationFactory.WithBrokenVersion(
            realMigrations, BrokenVersion, BrokenUpScript);

        var runner = new SqliteBoxMigrationRunner(config);
        var staleHint = new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: SeedVersion);

        //Act + Assert (1) — broken V6 in chain: runner throws and rolls back everything.
        //Per ADR §5a SQLite is whole-chain transactional like MSSQL/Postgres — the BEGIN IMMEDIATE
        //transaction wraps EnsureHistoryTableAsync + bootstrap path + per-migration ALTER+history,
        //so a mid-chain failure rolls back V4 ALTER + V5 ALTER + their history rows + the
        //synthetic V3 history row + (if newly-created in this tx) the history table itself.
        await Assert.ThrowsAsync<SqliteException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, brokenMigrations, staleHint));

        //Assert — no history rows for this box (synthetic V3 + V4/V5 applied rows all rolled back).
        Assert.Empty(await GetHistoryRowsByVersion());

        //Assert — table still at V3 shape (V4 PartitionKey ALTER rolled back; V5+ never reached).
        var columnsAfterFailure = await GetTableColumns();
        Assert.Contains("ContentType", columnsAfterFailure); // V3 column present (pre-transactional)
        Assert.DoesNotContain("PartitionKey", columnsAfterFailure); // V4 column rolled back
        Assert.DoesNotContain("Source", columnsAfterFailure); // V5 column never applied

        //Assert — seeded marker row preserved (no DROP/recreate happened).
        Assert.Equal(1, await GetMarkerRowCount());

        //Act + Assert (2) — retry with the real migration list: bootstrap path completes V4..V7.
        var provisioner = new SqliteOutboxProvisioner(config, runner);
        await provisioner.ProvisionAsync();

        //Assert — exactly one synthetic V3 + one applied per V4..V7 (no duplicates).
        var rowsByVersion = await GetHistoryRowsByVersion();
        Assert.Equal(ExpectedMigrationVersions.OutboxLatest - SeedVersion + 1, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(SeedVersion, rowsByVersion);
        Assert.StartsWith($"bootstrap: detected at V{SeedVersion}", syntheticDescription);

        for (var v = SeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var appliedDescription = Assert.Contains(v, rowsByVersion);
            Assert.False(
                appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be an applied migration row, not a synthetic bootstrap row " +
                $"(description was: '{appliedDescription}')");
        }

        //Assert — table now at V7 shape with marker row still present.
        var columnsAfterRetry = await GetTableColumns();
        Assert.Contains("DataRef", columnsAfterRetry); // V7 column
        Assert.Contains("PartitionKey", columnsAfterRetry); // V4 column applied on retry
        Assert.Equal(1, await GetMarkerRowCount());
    }

    private async Task SeedMarkerRow()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO [{_tableName}] ([MessageId]) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetTableColumns()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info(@TableName)";
        command.Parameters.AddWithValue("@TableName", _tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private async Task<Dictionary<int, string>> GetHistoryRowsByVersion()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT [MigrationVersion], [Description] FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName
ORDER BY [MigrationVersion]";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);

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
            // history table does not exist — rolled back before EnsureHistoryTableAsync committed.
        }
        return rows;
    }

    private async Task<long> GetMarkerRowCount()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{_tableName}] WHERE [MessageId] = @MessageId";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            await dropTable.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText =
                "DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (SqliteException) { /* history table may not exist */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
