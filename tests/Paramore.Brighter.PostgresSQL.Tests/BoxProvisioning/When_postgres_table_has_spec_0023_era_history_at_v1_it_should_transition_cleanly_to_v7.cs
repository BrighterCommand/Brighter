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
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgres_table_has_spec_0023_era_history_at_v1_it_should_transition_cleanly_to_v7 : IAsyncLifetime
{
    private const string Spec0023EraDescription = "spec 0023 fresh install";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_transition_cleanly_via_normal_path_with_no_ddl_changes_and_no_duplicate_history()
    {
        //Arrange — V7-shaped outbox via the live builder DDL + spec-0023-era history at V1.
        new PostgresSqlTestHelper().SetupDatabase();
        await ExecuteDdl(PostgreSqlOutboxBuilder.GetDDL(_tableName, binaryMessagePayload: false));
        await EnsureHistoryTable();
        await SeedSpec0023EraHistoryRowAtV1();
        var columnsBefore = await GetTableColumns();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlOutboxProvisioner(config, runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — table shape unchanged (idempotent ADD COLUMN IF NOT EXISTS were no-ops).
        Assert.Equal(columnsBefore, await GetTableColumns());

        //Assert — V1 row preserved with original spec-0023-era description; V2..V7 inserted normally.
        var rowsByVersion = await GetHistoryRowsByVersion();
        Assert.Equal(ExpectedMigrationVersions.OutboxLatest, rowsByVersion.Count);
        Assert.Equal(Spec0023EraDescription, rowsByVersion[1]);

        for (var v = 2; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var description = Assert.Contains(v, rowsByVersion);
            Assert.False(
                description.StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be a normal-path applied row, not a synthetic bootstrap " +
                $"(description was: '{description}')");
            Assert.False(
                description.StartsWith("fresh install", StringComparison.Ordinal),
                $"V{v} should be a normal-path applied row, not a fresh-install marker " +
                $"(description was: '{description}')");
        }
    }

    private async Task ExecuteDdl(string sql)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
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

    private async Task SeedSpec0023EraHistoryRowAtV1()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO ""__BrighterMigrationHistory"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (1, 'public', @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@Description", Spec0023EraDescription);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetTableColumns()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT column_name FROM information_schema.columns
WHERE table_name = @TableName AND table_schema = 'public'";
        command.Parameters.AddWithValue("@TableName", _tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private async Task<Dictionary<int, string>> GetHistoryRowsByVersion()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT ""MigrationVersion"", ""Description"" FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'
ORDER BY ""MigrationVersion""";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);

        var rows = new Dictionary<int, string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS \"{_tableName}\"";
            await dropTable.ExecuteNonQueryAsync();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DELETE FROM ""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (PostgresException) { /* history table may not exist */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
