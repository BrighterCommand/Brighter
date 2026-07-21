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

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class InboxV1ToV2UpgradeTests
{
    private const string MarkerCommandId = "marker-command-must-survive";

    private static readonly string[] s_v2ExpectedColumns =
    [
        "CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"
    ];

    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _tableName = $"test_inbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_sqlite_inbox_table_is_bootstrapped_at_v1_it_should_upgrade_to_v2()
    {
        //Arrange — seed a V1 inbox (no ContextKey, no history) plus a marker command row.
        SqliteInboxLegacySeeder.SeedAtV1(_connectionString, _tableName);
        await SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: _tableName);
        var runner = new SqliteBoxMigrationRunner(new SqliteInboxMigrationCatalog(), config);
        var provisioner = new SqliteInboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteInboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — the table now has the V2 column set (ContextKey ALTER applied).
        var actualColumns = await GetTableColumns();
        foreach (var expected in s_v2ExpectedColumns)
        {
            await Assert.That(actualColumns).Contains(expected);
        }

        //Assert — history rows: one synthetic at V1 + one applied at V2.
        var rowsByVersion = await GetHistoryRowsByVersion();
        await Assert.That(rowsByVersion.Count).IsEqualTo(ExpectedMigrationVersions.InboxLatest);

        await Assert.That(rowsByVersion.ContainsKey(1)).IsTrue();

        var syntheticDescription = rowsByVersion[1];
        await Assert.That(syntheticDescription).StartsWith("bootstrap: detected at V1");

        await Assert.That(rowsByVersion.ContainsKey(2)).IsTrue();

        var appliedDescription = rowsByVersion[2];
        await Assert.That(appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse();

        //Assert — the seeded marker row survived with NULL ContextKey on the new column.
        await Assert.That(await MarkerRowExistsWithNullContextKey()).IsTrue();
    }

    private async Task SeedMarkerRow()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO [{_tableName}] ([CommandId]) VALUES (@CommandId)";
        command.Parameters.AddWithValue("@CommandId", MarkerCommandId);
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
            // history table absent — no rows to return
        }
        return rows;
    }

    private async Task<bool> MarkerRowExistsWithNullContextKey()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM [{_tableName}]
WHERE [CommandId] = @CommandId AND [ContextKey] IS NULL";
        command.Parameters.AddWithValue("@CommandId", MarkerCommandId);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1L;
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