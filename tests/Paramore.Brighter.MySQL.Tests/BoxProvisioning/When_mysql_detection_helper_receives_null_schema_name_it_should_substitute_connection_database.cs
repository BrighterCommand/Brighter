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
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlDetectionHelperNullSchemaTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_mysql_detection_helper_receives_null_schema_name_it_should_substitute_connection_database()
    {
        // Arrange — a box-shaped table in the connection's default database and a history row
        // recorded against SchemaName=<connection.Database>. The helper's null-substitution rule
        // (per ADR 0057 §A.1) must make a call with schemaName: null behave identically to a
        // call with schemaName: connection.Database — MySQL has no separate schema concept, so
        // the database name is the schema.
        var tableName = TrackTable($"nullschema_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE `{tableName}` (`Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, `HeaderBag` TEXT NULL) ENGINE = InnoDB;");

        var databaseName = DatabaseName();
        await EnsureHistoryTable();
        await SeedHistoryRow(tableName, schemaName: databaseName, migrationVersion: 3);

        var helper = new MySqlBoxDetectionHelper();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Act + Assert — DoesTableExistAsync: null and connection.Database must agree.
        var existsWithDatabase = await helper.DoesTableExistAsync(connection, tableName, databaseName);
        var existsWithNull = await helper.DoesTableExistAsync(connection, tableName, schemaName: null);
        Assert.True(existsWithDatabase);
        Assert.True(existsWithNull);

        // Act + Assert — DoesHistoryExistAsync: null must locate the row recorded against the database.
        var historyWithDatabase = await helper.DoesHistoryExistAsync(connection, tableName, databaseName);
        var historyWithNull = await helper.DoesHistoryExistAsync(connection, tableName, schemaName: null);
        Assert.True(historyWithDatabase);
        Assert.True(historyWithNull);

        // Act + Assert — GetMaxVersionAsync: null must read the same version recorded under the database.
        var maxWithDatabase = await helper.GetMaxVersionAsync(connection, tableName, databaseName);
        var maxWithNull = await helper.GetMaxVersionAsync(connection, tableName, schemaName: null);
        Assert.Equal(3, maxWithDatabase);
        Assert.Equal(3, maxWithNull);

        // Act + Assert — GetTableColumnsAsync: null must return the same column set as the database.
        var colsWithDatabase = await helper.GetTableColumnsAsync(connection, tableName, databaseName);
        var colsWithNull = await helper.GetTableColumnsAsync(connection, tableName, schemaName: null);
        Assert.Contains("HeaderBag", colsWithDatabase);
        Assert.Contains("HeaderBag", colsWithNull);
    }

    private async Task ExecuteDdl(string sql)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureHistoryTable()
    {
        await ExecuteDdl(@"
CREATE TABLE IF NOT EXISTS `__BrighterMigrationHistory` (
    `MigrationVersion` INT NOT NULL,
    `SchemaName` VARCHAR(64) NOT NULL,
    `BoxTableName` VARCHAR(64) NOT NULL,
    `Description` VARCHAR(512) NOT NULL,
    `AppliedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SchemaName`, `BoxTableName`, `MigrationVersion`)
) ENGINE = InnoDB;");
    }

    private async Task SeedHistoryRow(string boxTableName, string schemaName, int migrationVersion)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO `__BrighterMigrationHistory` (`MigrationVersion`, `SchemaName`, `BoxTableName`, `Description`)
VALUES (@MigrationVersion, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@MigrationVersion", migrationVersion);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", boxTableName);
        command.Parameters.AddWithValue("@Description", "spec 0028 phase 2.3 null-substitution test");
        await command.ExecuteNonQueryAsync();
    }

    private string TrackTable(string tableName)
    {
        _tablesToCleanup.Add(tableName);
        return tableName;
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(_connectionString);
        return builder.Database;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var tableName in _tablesToCleanup)
            {
                using var dropTable = connection.CreateCommand();
                dropTable.CommandText = $"DROP TABLE IF EXISTS `{tableName}`";
                await dropTable.ExecuteNonQueryAsync();

                using var deleteHistory = connection.CreateCommand();
                deleteHistory.CommandText =
                    "DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
                deleteHistory.Parameters.AddWithValue("@BoxTableName", tableName);
                try { await deleteHistory.ExecuteNonQueryAsync(); }
                catch (MySqlException) { /* history table may not exist */ }
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
