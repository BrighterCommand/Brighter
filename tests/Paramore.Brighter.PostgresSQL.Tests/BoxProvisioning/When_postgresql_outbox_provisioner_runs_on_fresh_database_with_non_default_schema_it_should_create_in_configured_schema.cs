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
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// Per PR #4039 reviewer item M4-1 (F1b): PG had the same fresh-install SchemaName bug
// as MSSQL — PostgreSqlOutboxBuilder.GetDDL produced `CREATE TABLE IF NOT EXISTS {table}`
// (unqualified) so the table landed in the connection's search_path default (`public`)
// regardless of the configured SchemaName. V2..V7 ALTERs use `{schema}.{table}`, the
// detection helper queries `INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @SchemaName`,
// and the box-table state ended up partially-in-public, history-says-vN-in-billing, and
// drift on a second run.
//
// This test pre-creates a non-default schema, runs the provisioner with SchemaName set,
// and asserts the table actually lives in that schema (not in `public`).
public class When_postgresql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _nonDefaultSchema = $"billing_for_schema_test_{Guid.NewGuid():N}";
    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public When_postgresql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema()
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _nonDefaultSchema);
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_create_outbox_in_configured_schema_and_no_op_on_second_run()
    {
        //Arrange — non-default schema is pre-created (PG runner does not create schemas itself).
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_nonDefaultSchema);
        await DropAnyExistingTableAsync(_tableName, _nonDefaultSchema);
        await DropAnyExistingTableAsync(_tableName, "public");

        //Act — first fresh-install run
        var firstException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert — table lives in the configured schema (not `public`).
        Assert.Null(firstException);
        Assert.True(
            await TableExistsInSchemaAsync(_tableName, _nonDefaultSchema),
            $"Outbox table '{_tableName}' must exist in '{_nonDefaultSchema}' after fresh install with SchemaName='{_nonDefaultSchema}'.");
        Assert.False(
            await TableExistsInSchemaAsync(_tableName, "public"),
            $"Outbox table '{_tableName}' must NOT exist in 'public' when SchemaName='{_nonDefaultSchema}' is configured.");

        Assert.Equal(1, await GetHistoryRowCountAsync(_nonDefaultSchema, _tableName));

        //Act — second run on a provisioned database
        var secondException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert — idempotent
        Assert.Null(secondException);
        Assert.True(await TableExistsInSchemaAsync(_tableName, _nonDefaultSchema));
        Assert.Equal(1, await GetHistoryRowCountAsync(_nonDefaultSchema, _tableName));
    }

    private async Task EnsureSchemaExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{schemaName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropAnyExistingTableAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP TABLE IF EXISTS ""{schemaName}"".""{tableName}""";
        await command.ExecuteNonQueryAsync();
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

    private async Task<long> GetHistoryRowCountAsync(string schemaName, string tableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var dropSchemaCmd = connection.CreateCommand();
            dropSchemaCmd.CommandText = $@"DROP SCHEMA IF EXISTS ""{_nonDefaultSchema}"" CASCADE";
            await dropSchemaCmd.ExecuteNonQueryAsync();
            await using var dropPublicTable = connection.CreateCommand();
            dropPublicTable.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{_tableName}""";
            await dropPublicTable.ExecuteNonQueryAsync();
            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'public' AND table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""public"".""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName;
    END IF;
END
$$;";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
