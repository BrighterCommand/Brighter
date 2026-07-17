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

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

// Per PR #4039 reviewer item M4-1 (F1c): on MySQL, "schema" is synonymous with "database".
// The pre-F1c code path emitted unqualified `CREATE TABLE IF NOT EXISTS {table}` which
// landed the table in the connection's bound database (DATABASE()) regardless of the
// configured SchemaName. V2..V7 ALTERs used unqualified `{table}` and
// `information_schema.columns WHERE table_schema = DATABASE()`, while the detection
// helper looked in the configured SchemaName — so SchemaName != connection.Database
// produced a silent split: detection says "no table" but creation re-attempts the
// CREATE TABLE in DATABASE() which already has it.
//
// This test pre-creates a non-default MySQL database (the MySQL analogue of a schema),
// configures SchemaName to that database name, and asserts the table lands there.
public class MySqlOutboxNonDefaultSchemaTests
{
    private readonly string _baseConnectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _nonDefaultDatabase = $"brighter_billing_{Guid.NewGuid():N}";
    private MySqlOutboxProvisioner _provisioner = default!;
    private string _connectionInDefaultDb = default!;

    [Before(Test)]
    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync(_nonDefaultDatabase);
        // The provisioner's runner connects to the connection-string's Database
        // (BrighterTests) and emits schema-qualified DDL referencing _nonDefaultDatabase.
        // This separation pins the contract: SchemaName drives placement, not the
        // connection's bound database.
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

    [Test]
    public async Task When_mysql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema()
    {
        //Arrange
        await DropAnyExistingTableAsync(_tableName, _nonDefaultDatabase);
        await DropAnyExistingTableAsync(_tableName, "BrighterTests");

        //Act — first fresh-install run
        var firstException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — table lives in the configured database (not the connection's bound DB)
        await Assert.That(firstException).IsNull();
        await Assert.That(await TableExistsInDatabaseAsync(_tableName, _nonDefaultDatabase)).IsTrue().Because($"Outbox table '{_tableName}' must exist in '{_nonDefaultDatabase}' after fresh install with SchemaName='{_nonDefaultDatabase}'.");
        await Assert.That(await TableExistsInDatabaseAsync(_tableName, "BrighterTests")).IsFalse().Because($"Outbox table '{_tableName}' must NOT exist in BrighterTests (connection's default DB) when SchemaName='{_nonDefaultDatabase}' is configured.");
        await Assert.That(await GetHistoryRowCountAsync(_nonDefaultDatabase, _tableName)).IsEqualTo(1);

        //Act — idempotent second run
        var secondException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert
        await Assert.That(secondException).IsNull();
        await Assert.That(await TableExistsInDatabaseAsync(_tableName, _nonDefaultDatabase)).IsTrue();
        await Assert.That(await GetHistoryRowCountAsync(_nonDefaultDatabase, _tableName)).IsEqualTo(1);
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
        // History table lives in the connection's bound database (BrighterTests) per
        // existing ADR contract — only the box table is in the configured schema.
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

    [After(Test)]
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
