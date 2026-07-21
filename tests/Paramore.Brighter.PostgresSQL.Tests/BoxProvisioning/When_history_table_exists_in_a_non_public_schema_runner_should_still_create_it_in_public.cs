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

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// This scenario owns an isolated database because it must control whether the public history
// table exists without affecting parallel provisioning tests.
public class PostgreSqlHistoryTableNonPublicSchemaTests
{
    private const string CollidingSchema = "stage_for_history_clash_test";
    private readonly string _databaseName = $"brighter_history_clash_{Guid.NewGuid():N}";
    private readonly string _setupConnectionString;
    private readonly string _runnerConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public PostgreSqlHistoryTableNonPublicSchemaTests()
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(PostgreSqlSettings.TestsMasterConnectionString)
        {
            Database = _databaseName,
            Pooling = false
        };
        _setupConnectionString = connectionStringBuilder.ConnectionString;

        // Force the runner's connection to put the colliding schema first on search_path.
        // This is the trigger condition for the bug: an unqualified CREATE TABLE / INSERT /
        // SELECT against "__BrighterMigrationHistory" resolves to whichever schema appears
        // first on search_path that has (or can hold) the relation.
        _runnerConnectionString = _setupConnectionString.TrimEnd(';') + $";Search Path={CollidingSchema},public";
        var config = new RelationalDatabaseConfiguration(_runnerConnectionString, outBoxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_history_table_exists_in_a_non_public_schema_runner_should_still_create_it_in_public()
    {
        //Arrange — pre-create stage_for_history_clash_test."__BrighterMigrationHistory" with a
        //deliberately wrong shape. With CollidingSchema first on the runner connection's
        //search_path the unqualified CREATE TABLE IF NOT EXISTS resolves to that schema, sees
        //the existing relation, and skips the create — leaving subsequent unqualified
        //INSERT/SELECT statements to land in the wrong schema (or fail outright).
        await CreateIsolatedDatabase();
        await CreateCollidingSchemaAndHistoryTable();

        //Act
        var act = async () => await _provisioner.ProvisionAsync();
        var ex = await TestExceptionRecorder.CaptureAsync(act);

        //Assert — runner must succeed, "public"."__BrighterMigrationHistory" must exist with
        //the correct shape and one V_latest fresh-install row, and the colliding stage table
        //must be untouched.
        await Assert.That(ex).IsNull();
        await Assert.That(await PublicHistoryTableExists()).IsTrue().Because(@"""public"".""__BrighterMigrationHistory"" must be created");
        await Assert.That(await GetPublicHistoryRowCountForBox()).IsEqualTo(1L);
        await Assert.That(await GetCollidingHistoryRowCount()).IsEqualTo(0L);
    }

    private Task CreateIsolatedDatabase() =>
        ExecuteNonQuery(
            PostgreSqlSettings.TestsMasterConnectionString,
            $@"CREATE DATABASE ""{_databaseName}""");

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

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await ExecuteNonQuery(
                PostgreSqlSettings.TestsMasterConnectionString,
                $@"DROP DATABASE IF EXISTS ""{_databaseName}"" WITH (FORCE)");
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
