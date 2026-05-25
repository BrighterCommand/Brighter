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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.Legacy;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class When_mysql_inbox_table_is_bootstrapped_at_v1_it_should_upgrade_to_v2 : IAsyncLifetime
{
    private const string MarkerCommandId = "marker-command-must-survive";

    private static readonly string[] s_v2ExpectedColumns =
    [
        "CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"
    ];

    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_inbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_add_contextkey_synthetic_v1_and_applied_v2_history()
    {
        //Arrange — seed a V1 inbox (no ContextKey, no history) plus a marker command row.
        MySqlInboxLegacySeeder.SeedAtV1(_connectionString, _tableName);
        await SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: _tableName);
        var runner = new MySqlBoxMigrationRunner(new MySqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlInboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlInboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — the table now has the V2 column set (ContextKey ALTER applied)
        var actualColumns = await GetTableColumns();
        foreach (var expected in s_v2ExpectedColumns)
        {
            Assert.Contains(expected, actualColumns);
        }

        //Assert — history rows: one synthetic at V1 + one applied at V2
        var rowsByVersion = await GetHistoryRowsByVersion();
        Assert.Equal(2, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(1, rowsByVersion);
        Assert.StartsWith("bootstrap: detected at V1", syntheticDescription);

        var appliedDescription = Assert.Contains(2, rowsByVersion);
        Assert.False(
            appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
            $"V2 should be an applied migration row, not a synthetic bootstrap row " +
            $"(description was: '{appliedDescription}')");

        //Assert — the seeded marker row survived with NULL ContextKey on the new column
        Assert.True(await MarkerRowExistsWithNullContextKey());
    }

    private async Task SeedMarkerRow()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO `{_tableName}` (`CommandId`, `CommandType`, `CommandBody`, `Timestamp`) " +
            "VALUES (@CommandId, 'TestCommand', '{}', NOW(4))";
        command.Parameters.AddWithValue("@CommandId", MarkerCommandId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetTableColumns()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM information_schema.columns
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName";
        command.Parameters.AddWithValue("@TableName", _tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private async Task<Dictionary<int, string>> GetHistoryRowsByVersion()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT `MigrationVersion`, `Description` FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName
ORDER BY `MigrationVersion`";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);

        var rows = new Dictionary<int, string>();
        try
        {
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows[reader.GetInt32(0)] = reader.GetString(1);
            }
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            // history table absent — no rows to return
        }
        return rows;
    }

    private async Task<bool> MarkerRowExistsWithNullContextKey()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM `{_tableName}`
WHERE `CommandId` = @CommandId AND `ContextKey` IS NULL";
        command.Parameters.AddWithValue("@CommandId", MarkerCommandId);
        return (long)(await command.ExecuteScalarAsync())! == 1L;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS `{_tableName}`";
            await dropTable.ExecuteNonQueryAsync();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText =
                "DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (MySqlException) { /* history table may not exist */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
