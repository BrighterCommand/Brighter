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
using Paramore.Brighter.Outbox.Sqlite;
using TUnit.Assertions.Enums;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class Spec0023EraHistoryTransitionTests
{
    private const string Spec0023EraDescription = "spec 0023 fresh install";

    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_sqlite_table_has_spec_0023_era_history_at_v1_it_should_transition_cleanly_to_v_latest()
    {
        //Arrange — V7-shaped outbox via the live builder DDL + spec-0023-era history at V1.
        //This models a prod install that was provisioned under spec 0023 (which only ever
        //stamped V_latest=1 against the live builder shape) — the V1 row is honest "I made
        //this table" history but its description text is whatever spec 0023 chose ("spec 0023
        //fresh install"), not the V_k descriptions spec 0027 introduces.
        await ExecuteDdl(SqliteOutboxBuilder.GetDDL(_tableName, hasBinaryMessagePayload: false));
        await EnsureHistoryTable();
        await SeedSpec0023EraHistoryRowAtV1();
        var columnsBefore = await GetTableColumns();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var runner = new SqliteBoxMigrationRunner(new SqliteOutboxMigrationCatalog(), config);
        var provisioner = new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — table shape unchanged. The runner's normal path walks V2..V7; per ADR §6,
        //ApplyOrSkipAsync evaluates IdempotencyCheckSql (pragma_table_info probe) for each
        //V_k — returns >0 because the V_latest builder shape already has the column — so
        //UpScript is skipped, history row inserted only.
        await Assert.That(await GetTableColumns()).IsEquivalentTo(columnsBefore, CollectionOrdering.Matching);

        //Assert — V1 row preserved with its original spec-0023-era description (the runner's
        //normal-path `migration.Version <= maxVersion` filter sees V1 <= maxVersion=1 and skips
        //it entirely); V2..V7 inserted by the normal path with their per-migration descriptions.
        var rowsByVersion = await GetHistoryRowsByVersion();
        await Assert.That(rowsByVersion.Count).IsEqualTo(ExpectedMigrationVersions.OutboxLatest);
        await Assert.That(rowsByVersion[1]).IsEqualTo(Spec0023EraDescription);

        for (var v = 2; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            await Assert.That(rowsByVersion.ContainsKey(v)).IsTrue();
            var description = rowsByVersion[v];
            await Assert.That(description.StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse();
            await Assert.That(description.StartsWith("fresh install", StringComparison.Ordinal)).IsFalse();
        }
    }

    private async Task ExecuteDdl(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private Task EnsureHistoryTable() =>
        ExecuteDdl(@"
CREATE TABLE IF NOT EXISTS [__BrighterMigrationHistory] (
    [MigrationVersion] INTEGER NOT NULL,
    [BoxTableName] TEXT NOT NULL,
    [Description] TEXT NOT NULL,
    [AppliedAt] TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY ([BoxTableName], [MigrationVersion])
)");

    private async Task SeedSpec0023EraHistoryRowAtV1()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO [__BrighterMigrationHistory] ([MigrationVersion], [BoxTableName], [Description])
VALUES (1, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@Description", Spec0023EraDescription);
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
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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
