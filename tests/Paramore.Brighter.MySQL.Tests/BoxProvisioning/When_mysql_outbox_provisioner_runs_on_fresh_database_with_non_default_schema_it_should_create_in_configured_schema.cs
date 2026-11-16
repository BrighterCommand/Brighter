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

#nullable enable

using System;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// On MySQL a schema is a database: with <c>SchemaName</c> set to a database other than the connection's,
/// a fresh install creates the outbox there rather than in the connection's database, and a second run is
/// a no-op (ADR 0057 §3, ADR 0060). The test creates that database: the runner does not create box schemas.
/// </summary>
public class MySqlOutboxNonDefaultSchemaTests : IAsyncLifetime
{
    private readonly string _baseConnectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _nonDefaultDatabase = $"brighter_billing_{Guid.NewGuid():N}";
    private MySqlOutboxProvisioner _provisioner = default!;
    private string _connectionInDefaultDb = default!;

    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync(_nonDefaultDatabase);
        _connectionInDefaultDb = _baseConnectionString;

        var config = new RelationalDatabaseConfiguration(
            _connectionInDefaultDb,
            outBoxTableName: _tableName,
            schemaName: _nonDefaultDatabase);
        var runner = new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);
    }

    [Fact]
    public async Task When_mysql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema()
    {
        //Arrange
        await DropAnyExistingTableAsync(_tableName, _nonDefaultDatabase);
        await DropAnyExistingTableAsync(_tableName, "BrighterTests");

        //Act
        var firstException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert
        Assert.Null(firstException);
        Assert.True(
            await TableExistsInDatabaseAsync(_tableName, _nonDefaultDatabase),
            $"Outbox table '{_tableName}' must exist in '{_nonDefaultDatabase}' after fresh install with SchemaName='{_nonDefaultDatabase}'.");
        Assert.False(
            await TableExistsInDatabaseAsync(_tableName, "BrighterTests"),
            $"Outbox table '{_tableName}' must NOT exist in BrighterTests (connection's default DB) when SchemaName='{_nonDefaultDatabase}' is configured.");
        Assert.Equal(1, await GetHistoryRowCountAsync(_nonDefaultDatabase, _tableName));

        //Act
        var secondException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert
        Assert.Null(secondException);
        Assert.True(await TableExistsInDatabaseAsync(_tableName, _nonDefaultDatabase));
        Assert.Equal(1, await GetHistoryRowCountAsync(_nonDefaultDatabase, _tableName));
    }

    private async Task EnsureDatabaseExistsAsync(string databaseName)
    {
        await using var connection = new MySqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}`";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseIfExistsAsync(string databaseName)
    {
        await using var connection = new MySqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{databaseName}`";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropAnyExistingTableAsync(string tableName, string databaseName)
    {
        await using var connection = new MySqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS `{databaseName}`.`{tableName}`";
        try { await command.ExecuteNonQueryAsync(); }
        catch { /* DB may not exist yet */ }
    }

    private async Task<bool> TableExistsInDatabaseAsync(string tableName, string databaseName)
    {
        await using var connection = new MySqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM information_schema.tables
WHERE table_schema = @SchemaName AND table_name = @TableName";
        command.Parameters.AddWithValue("@SchemaName", databaseName);
        command.Parameters.AddWithValue("@TableName", tableName);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());
        return count > 0;
    }

    private async Task<long> GetHistoryRowCountAsync(string schemaName, string tableName)
    {
        await using var connection = new MySqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `SchemaName` = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task DisposeAsync()
    {
        try
        {
            await DropAnyExistingTableAsync(_tableName, _nonDefaultDatabase);
            await DropAnyExistingTableAsync(_tableName, "BrighterTests");
            await DropDatabaseIfExistsAsync(_nonDefaultDatabase);
            await using var connection = new MySqlConnection(_baseConnectionString);
            await connection.OpenAsync();
            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); } catch { }
        }
        catch { /* best-effort */ }
    }
}
