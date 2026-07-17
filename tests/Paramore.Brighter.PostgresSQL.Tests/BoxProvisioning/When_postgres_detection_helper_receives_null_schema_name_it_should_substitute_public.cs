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
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlDetectionHelperNullSchemaTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly List<string> _tablesToCleanup = [];

    [Test]
    public async Task When_postgres_detection_helper_receives_null_schema_name_it_should_substitute_public()
    {
        // Arrange — a box-shaped table in the public schema and a history row recorded against
        // SchemaName='public'. The helper's null-substitution rule (per ADR 0057 §A.1) must make
        // a call with schemaName: null behave identically to a call with schemaName: "public".
        new PostgresSqlTestHelper().SetupDatabase();
        var tableName = TrackTable($"nullschema_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE \"{tableName}\" (id BIGSERIAL PRIMARY KEY, headerbag TEXT NULL);");

        await EnsureHistoryTable();
        await SeedHistoryRow(tableName, schemaName: "public", migrationVersion: 3);

        var helper = new PostgreSqlBoxDetectionHelper();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Act + Assert — DoesTableExistAsync: null and "public" must agree.
        var existsWithPublic = await helper.DoesTableExistAsync(connection, tableName, "public");
        var existsWithNull = await helper.DoesTableExistAsync(connection, tableName, schemaName: null);
        await Assert.That(existsWithPublic).IsTrue();
        await Assert.That(existsWithNull).IsTrue();

        // Act + Assert — DoesHistoryExistAsync: null must locate the row recorded against 'public'.
        // historySchema: null keeps the history table in the backend default ('public') — today's behaviour.
        var historyWithPublic = await helper.DoesHistoryExistAsync(connection, tableName, "public", historySchema: null);
        var historyWithNull = await helper.DoesHistoryExistAsync(connection, tableName, schemaName: null, historySchema: null);
        await Assert.That(historyWithPublic).IsTrue();
        await Assert.That(historyWithNull).IsTrue();

        // Act + Assert — GetMaxVersionAsync: null must read the same version recorded under 'public'.
        var maxWithPublic = await helper.GetMaxVersionAsync(connection, tableName, "public", historySchema: null);
        var maxWithNull = await helper.GetMaxVersionAsync(connection, tableName, schemaName: null, historySchema: null);
        await Assert.That(maxWithPublic).IsEqualTo(3);
        await Assert.That(maxWithNull).IsEqualTo(3);

        // Act + Assert — GetTableColumnsAsync: null must return the same column set as 'public'.
        var colsWithPublic = await helper.GetTableColumnsAsync(connection, tableName, "public");
        var colsWithNull = await helper.GetTableColumnsAsync(connection, tableName, schemaName: null);
        await Assert.That(colsWithPublic).Contains("headerbag");
        await Assert.That(colsWithNull).Contains("headerbag");
    }

    private async Task ExecuteDdl(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureHistoryTable()
    {
        await ExecuteDdl(@"
CREATE TABLE IF NOT EXISTS ""__BrighterMigrationHistory"" (
    ""MigrationVersion"" INT NOT NULL,
    ""SchemaName"" VARCHAR(256) NOT NULL DEFAULT 'public',
    ""BoxTableName"" VARCHAR(256) NOT NULL,
    ""Description"" VARCHAR(512) NOT NULL,
    ""AppliedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (""SchemaName"", ""BoxTableName"", ""MigrationVersion"")
)");
    }

    private async Task SeedHistoryRow(string boxTableName, string schemaName, int migrationVersion)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO ""__BrighterMigrationHistory"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (@MigrationVersion, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@MigrationVersion", migrationVersion);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", boxTableName);
        command.Parameters.AddWithValue("@Description", "spec 0028 phase 2.2 null-substitution test");
        await command.ExecuteNonQueryAsync();
    }

    private string TrackTable(string tableName)
    {
        _tablesToCleanup.Add(tableName);
        return tableName;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var tableName in _tablesToCleanup)
            {
                await using var dropTable = connection.CreateCommand();
                dropTable.CommandText = $"DROP TABLE IF EXISTS \"{tableName}\"";
                await dropTable.ExecuteNonQueryAsync();
            }

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""__BrighterMigrationHistory"" WHERE ""BoxTableName"" LIKE 'nullschema_%';
    END IF;
END
$$;";
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}