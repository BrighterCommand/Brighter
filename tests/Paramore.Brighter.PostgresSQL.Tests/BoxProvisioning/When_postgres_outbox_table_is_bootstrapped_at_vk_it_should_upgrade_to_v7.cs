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
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.Legacy;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgres_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7 : IAsyncLifetime
{
    private static readonly string[] s_v7ExpectedColumns =
    [
        "id", "messageid", "topic", "messagetype", "timestamp", "headerbag", "body",
        "dispatched", "correlationid", "replyto", "contenttype", "partitionkey",
        "source", "type", "dataschema", "subject", "traceparent", "tracestate", "baggage",
        "workflowid", "jobid", "dataref", "specversion"
    ];

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public async Task Should_upgrade_to_v7_with_synthetic_v_k_plus_applied_rows(int k)
    {
        //Arrange — seed an outbox at V_k (no history row) and a marker row to prove preservation.
        new PostgresSqlTestHelper().SetupDatabase();
        PostgreSqlOutboxLegacySeeder.SeedAtV(k, _connectionString, _tableName);
        SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — the table now has the full V7 column set (V_{k+1}..V7 ALTERs applied)
        var actualColumns = await GetTableColumns();
        foreach (var expected in s_v7ExpectedColumns)
        {
            Assert.Contains(expected, actualColumns);
        }

        //Assert — history rows: one synthetic at V_k + one applied per V_{k+1}..V7
        var rowsByVersion = await GetHistoryRowsByVersion();
        Assert.Equal(7 - k + 1, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(k, rowsByVersion);
        Assert.StartsWith($"bootstrap: detected at V{k}", syntheticDescription);

        for (var v = k + 1; v <= 7; v++)
        {
            var appliedDescription = Assert.Contains(v, rowsByVersion);
            Assert.False(
                appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be an applied migration row, not a synthetic bootstrap row " +
                $"(description was: '{appliedDescription}')");
        }

        //Assert — the seeded marker row survived (no DROP/recreate happened)
        Assert.Equal(1, await GetMarkerRowCount());
    }

    private void SeedMarkerRow()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO \"{_tableName}\" (messageid) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", "marker-row-must-survive");
        command.ExecuteNonQuery();
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

    private async Task<long> GetMarkerRowCount()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM \"{_tableName}\" WHERE messageid = @MessageId";
        command.Parameters.AddWithValue("@MessageId", "marker-row-must-survive");
        return (long)(await command.ExecuteScalarAsync())!;
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
            catch (PostgresException) { /* history table may not exist if runner rolled back */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
