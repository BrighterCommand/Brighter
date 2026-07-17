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
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

// Spec 0029 FR1b/AC1b (ADR 0060 D3): SQLite has no schema concept, so it inherits the base
// default SupportsPerSchemaHistory => false. Selecting MigrationHistoryScope.PerSchema must be
// a NO-OP: history lives in __BrighterMigrationHistory in the single database file (as under
// Global), and the D3 "PerSchema + null SchemaName" guard MUST NOT fire (it only fires on
// placement backends). These characterization tests pin that no-op for both a non-null
// SchemaName (which SQLite accepts and ignores) and a null SchemaName.
public class SqlitePerSchemaNoOpTests
{
    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_per_schema_is_selected_with_a_non_null_schema_it_should_be_a_no_op_and_not_throw()
    {
        //Arrange — PerSchema scope + a non-null SchemaName. SQLite ignores schema names entirely.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: "billing");
        var provisioner = BuildProvisioner(config);

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(() => provisioner.ProvisionAsync());

        //Assert — no throw and history is in the single database file (no placement change).
        await Assert.That(exception).IsNull();
        await Assert.That(await TableCountAsync(_tableName)).IsEqualTo(1);
        await Assert.That(await HistoryRowCountAsync()).IsEqualTo(1);
    }

    [Test]
    public async Task When_per_schema_is_selected_with_a_null_schema_it_should_be_a_no_op_and_not_throw()
    {
        //Arrange — PerSchema scope + a null SchemaName. On a placement backend this would trip the
        // D3 guard; on SQLite it must not.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: null);
        var provisioner = BuildProvisioner(config);

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(() => provisioner.ProvisionAsync());

        //Assert — no ConfigurationException (D3 guard is gated off for SQLite).
        await Assert.That(exception).IsNull();
        await Assert.That(await TableCountAsync(_tableName)).IsEqualTo(1);
        await Assert.That(await HistoryRowCountAsync()).IsEqualTo(1);
    }

    private SqliteOutboxProvisioner BuildProvisioner(RelationalDatabaseConfiguration config)
    {
        // Evident data: PerSchema is the scope under test.
        var runner = new SqliteBoxMigrationRunner(
            new SqliteOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        return new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);
    }

    private async Task<long> TableCountAsync(string tableName)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName";
        command.Parameters.AddWithValue("@TableName", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task<long> HistoryRowCountAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
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
            deleteHistory.CommandText = "DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); } catch { }
        }
        catch { }
    }
}
