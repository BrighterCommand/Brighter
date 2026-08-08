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
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// Spec 0029 FR1a / AC1a (ADR 0060 D3): on a placement backend (PostgreSQL), selecting
// MigrationHistoryScope.PerSchema with a null SchemaName is a misconfiguration — there is no
// schema to place history in. The runner must reject it at the MigrateAsync entry with a
// ConfigurationException and must NOT silently fall back to Global or create any history.
public class PostgreSqlPerSchemaNullSchemaNameTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly PostgreSqlBoxMigrationRunner _runner;

    public PostgreSqlPerSchemaNullSchemaNameTests()
    {
        // Evident data: PerSchema scope with a null SchemaName is the misconfiguration under test.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: null);
        _runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
    }

    [Fact]
    public async Task When_postgres_per_schema_scope_is_selected_with_null_schema_name_it_should_throw_configuration_exception()
    {
        //Arrange — a real database; no box table for this run yet.
        new PostgresSqlTestHelper().SetupDatabase();

        //Act
        var exception = await Record.ExceptionAsync(() => _runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, new BoxTableState(false, false, 0)));

        //Assert — rejected with a ConfigurationException naming the cause.
        var configException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains("PerSchema", configException.Message);
        Assert.Contains("SchemaName", configException.Message);

        //Assert — no silent fall-back to Global: nothing was created/recorded for this box.
        Assert.False(
            await TableExistsInSchemaAsync(_tableName, "public"),
            $"No box table '{_tableName}' should be created when the run is rejected.");
        Assert.Equal(0, await GetHistoryRowCountAsync(_tableName));
    }

    private async Task<bool> TableExistsInSchemaAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> GetHistoryRowCountAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // The shared history table may not exist on a clean database; treat "absent" as zero rows.
        command.CommandText = @"
SELECT CASE WHEN EXISTS(SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = '__BrighterMigrationHistory')
            THEN (SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName)
            ELSE 0 END";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task DropAnyExistingTableAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP TABLE IF EXISTS ""{schemaName}"".""{tableName}""";
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await DropAnyExistingTableAsync(_tableName, "public");
        }
        catch { /* best-effort cleanup */ }
    }
}
