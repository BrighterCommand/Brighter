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
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

// Spec 0029 FR1b/AC1b (ADR 0060 D3): on MySQL, "schema" is synonymous with "database", so
// per-schema history placement is not a distinct concept — MySQL inherits the base default
// SupportsPerSchemaHistory => false. Selecting MigrationHistoryScope.PerSchema must therefore
// be a NO-OP: history stays in the connection's bound database (DATABASE(), as under Global),
// and the D3 "PerSchema + null SchemaName" guard MUST NOT fire (it only fires on placement
// backends). These characterization tests pin that no-op for both a non-null SchemaName and a
// null SchemaName, so a future change that wrongly made MySQL honour PerSchema (or throw on
// null) is caught.
public class MySqlPerSchemaNoOpTests
{
    private const string ConnectionDatabase = "BrighterTests";
    private readonly string _baseConnectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    // Evident data: a non-default database is the MySQL analogue of a schema. Under PerSchema
    // this must NOT pull the history table out of the connection's bound database.
    private readonly string _nonDefaultDatabase = $"brighter_billing_{Guid.NewGuid():N}";

    [Before(Test)]
    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync(_nonDefaultDatabase);
    }

    [Test]
    public async Task When_per_schema_is_selected_with_a_non_null_schema_it_should_keep_history_in_connection_database_and_not_throw()
    {
        //Arrange — PerSchema scope + a non-null SchemaName (a non-default database).
        await DropAnyExistingTableAsync(_tableName, _nonDefaultDatabase);
        await DropAnyExistingTableAsync(_tableName, ConnectionDatabase);

        var config = new RelationalDatabaseConfiguration(
            _baseConnectionString,
            outBoxTableName: _tableName,
            schemaName: _nonDefaultDatabase);
        var provisioner = BuildProvisioner(config);

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(() => provisioner.ProvisionAsync());

        //Assert — no throw: the D3 guard does not apply to MySQL (SupportsPerSchemaHistory == false).
        await Assert.That(exception).IsNull();

        //Assert — history stays in the connection's bound database, NOT the configured one (no-op).
        await Assert.That(await TableExistsInDatabaseAsync("__BrighterMigrationHistory", ConnectionDatabase)).IsTrue().Because("History must remain in the connection's bound database under PerSchema (no-op).");
        await Assert.That(await TableExistsInDatabaseAsync("__BrighterMigrationHistory", _nonDefaultDatabase)).IsFalse().Because("PerSchema must NOT place history into the configured schema on MySQL.");
        await Assert.That(await GetHistoryRowCountInConnectionDatabaseAsync(_nonDefaultDatabase)).IsEqualTo(1);
    }

    [Test]
    public async Task When_per_schema_is_selected_with_a_null_schema_it_should_keep_history_in_connection_database_and_not_throw()
    {
        //Arrange — PerSchema scope + a null SchemaName. On a placement backend this would trip
        // the D3 guard; on MySQL it must not.
        await DropAnyExistingTableAsync(_tableName, ConnectionDatabase);

        var config = new RelationalDatabaseConfiguration(
            _baseConnectionString,
            outBoxTableName: _tableName,
            schemaName: null);
        var provisioner = BuildProvisioner(config);

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(() => provisioner.ProvisionAsync());

        //Assert — no ConfigurationException (D3 guard is gated off for MySQL).
        await Assert.That(exception).IsNull();

        //Assert — history lands in the connection's bound database (the Global location).
        await Assert.That(await TableExistsInDatabaseAsync("__BrighterMigrationHistory", ConnectionDatabase)).IsTrue();
        await Assert.That(await GetHistoryRowCountInConnectionDatabaseAsync(ConnectionDatabase)).IsEqualTo(1);
    }

    private MySqlOutboxProvisioner BuildProvisioner(RelationalDatabaseConfiguration config)
    {
        // Evident data: PerSchema is the scope under test.
        var runner = new MySqlBoxMigrationRunner(
            new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        return new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);
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

    private async Task<long> GetHistoryRowCountInConnectionDatabaseAsync(string boxSchemaName)
    {
        // History lives in the connection's bound database (BrighterTests); rows are filtered by
        // the box table's SchemaName, which the MySQL helper substitutes with DATABASE() when null.
        await using var connection = new MySqlConnection(_baseConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName AND `SchemaName` = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@SchemaName", boxSchemaName);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await DropAnyExistingTableAsync(_tableName, _nonDefaultDatabase);
            await DropAnyExistingTableAsync(_tableName, ConnectionDatabase);
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
