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
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// REQUIRES SEQUENTIAL EXECUTION (see PROMPT.md / branch convention) — temporarily drops the
// shared "public"."__BrighterMigrationHistory" table to demonstrate the schema-qualification
// bug. DisposeAsync drops the colliding artefacts; the runner naturally recreates the public
// history table so the rest of the BoxProvisioning suite is left in a consistent state.
public class When_history_table_exists_in_a_non_public_schema_runner_should_still_create_it_in_public : IAsyncLifetime
{
    private const string CollidingSchema = "stage_for_history_clash_test";
    private readonly string _setupConnectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _runnerConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public When_history_table_exists_in_a_non_public_schema_runner_should_still_create_it_in_public()
    {
        // Force the runner's connection to put the colliding schema first on search_path.
        // This is the trigger condition for the bug: an unqualified CREATE TABLE / INSERT /
        // SELECT against "__BrighterMigrationHistory" resolves to whichever schema appears
        // first on search_path that has (or can hold) the relation.
        _runnerConnectionString = _setupConnectionString.TrimEnd(';') + $";Search Path={CollidingSchema},public";
        var config = new RelationalDatabaseConfiguration(_runnerConnectionString, outBoxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlOutboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_create_history_table_in_public_regardless_of_search_path()
    {
        //Arrange — pre-create stage_for_history_clash_test."__BrighterMigrationHistory" with a
        //deliberately wrong shape. With CollidingSchema first on the runner connection's
        //search_path the unqualified CREATE TABLE IF NOT EXISTS resolves to that schema, sees
        //the existing relation, and skips the create — leaving subsequent unqualified
        //INSERT/SELECT statements to land in the wrong schema (or fail outright).
        new PostgresSqlTestHelper().SetupDatabase();
        await DropPublicHistoryTable();
        await DropCollidingArtefacts();
        await CreateCollidingSchemaAndHistoryTable();

        //Act
        var act = async () => await _provisioner.ProvisionAsync();
        var ex = await Record.ExceptionAsync(act);

        //Assert — runner must succeed, "public"."__BrighterMigrationHistory" must exist with
        //the correct shape and one V_latest fresh-install row, and the colliding stage table
        //must be untouched.
        Assert.Null(ex);
        Assert.True(await PublicHistoryTableExists(), @"""public"".""__BrighterMigrationHistory"" must be created");
        Assert.Equal(1L, await GetPublicHistoryRowCountForBox());
        Assert.Equal(0L, await GetCollidingHistoryRowCount());
    }

    private Task DropPublicHistoryTable() =>
        ExecuteNonQuery(_setupConnectionString,
            @"DROP TABLE IF EXISTS ""public"".""__BrighterMigrationHistory""");

    private async Task DropCollidingArtefacts()
    {
        await ExecuteNonQuery(_setupConnectionString,
            $@"DROP SCHEMA IF EXISTS ""{CollidingSchema}"" CASCADE");
    }

    private async Task CreateCollidingSchemaAndHistoryTable()
    {
        await ExecuteNonQuery(_setupConnectionString, $@"CREATE SCHEMA ""{CollidingSchema}""");
        await ExecuteNonQuery(_setupConnectionString, $@"
CREATE TABLE ""{CollidingSchema}"".""__BrighterMigrationHistory"" (
    ""bogus"" INT NOT NULL
)");
    }

    private async Task<bool> PublicHistoryTableExists()
    {
        await using var connection = new NpgsqlConnection(_setupConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM pg_tables " +
            "WHERE tablename = '__BrighterMigrationHistory' AND schemaname = 'public'";
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    private async Task<long> GetPublicHistoryRowCountForBox()
    {
        await using var connection = new NpgsqlConnection(_setupConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> GetCollidingHistoryRowCount()
    {
        await using var connection = new NpgsqlConnection(_setupConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"SELECT COUNT(1) FROM ""{CollidingSchema}"".""__BrighterMigrationHistory""";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteNonQuery(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await ExecuteNonQuery(_setupConnectionString, $@"DROP TABLE IF EXISTS ""public"".""{_tableName}""");
            await DropCollidingArtefacts();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
