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
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.Legacy;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlRunnerMidChainFailureRollbackTests : IAsyncLifetime
{
    private const int SeedVersion = 3;
    private const int BrokenVersion = 6;
    private const string BrokenUpScript =
        "DO $$ BEGIN RAISE EXCEPTION 'forced failure for spec 0027 task 2.7a'; END $$;";
    private const string MarkerMessageId = "marker-row-must-survive";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_postgres_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically()
    {
        //Arrange — seed an outbox at V3 (no history) plus a marker row to prove preservation.
        new PostgresSqlTestHelper().SetupDatabase();
        PostgreSqlOutboxLegacySeeder.SeedAtV(SeedVersion, _connectionString, _tableName);
        await SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realCatalog = new PostgreSqlOutboxMigrationCatalog();
        var realMigrations = realCatalog.All(config);
        var brokenMigrations = BrokenMigrationFactory.WithBrokenVersion(
            realMigrations, BrokenVersion, BrokenUpScript);
        var brokenCatalog = new BrokenChainCatalog(brokenMigrations, realCatalog.FreshInstallDdl(config));

        var brokenRunner = new PostgreSqlBoxMigrationRunner(brokenCatalog, config, TimeSpan.FromSeconds(30));
        var staleHint = new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: SeedVersion);

        //Act + Assert (1) — broken V6 in chain: runner throws and rolls back everything.
        await Assert.ThrowsAsync<PostgresException>(() => brokenRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint));

        //Assert — no history rows for this box (synthetic V3 + V4/V5 applied rows all rolled back).
        Assert.Empty(await GetHistoryRowsByVersion());

        //Assert — table still at V3 shape (V4 partitionkey ALTER rolled back; V5+ never reached).
        var columnsAfterFailure = await GetTableColumns();
        Assert.Contains("contenttype", columnsAfterFailure); // V3 column present
        Assert.DoesNotContain("partitionkey", columnsAfterFailure); // V4 column rolled back
        Assert.DoesNotContain("source", columnsAfterFailure); // V5 column never applied

        //Assert — seeded marker row preserved (no DROP/recreate happened).
        Assert.Equal(1, await GetMarkerRowCount());

        //Act + Assert (2) — retry with the real migration list: bootstrap path completes V4..V7.
        var realRunner = new PostgreSqlBoxMigrationRunner(realCatalog, config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            realRunner);
        await provisioner.ProvisionAsync();

        //Assert — exactly one synthetic V3 + one applied per V4..V7 (no duplicates).
        var rowsByVersion = await GetHistoryRowsByVersion();
        Assert.Equal(ExpectedMigrationVersions.OutboxLatest - SeedVersion + 1, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(SeedVersion, rowsByVersion);
        Assert.StartsWith($"bootstrap: detected at V{SeedVersion}", syntheticDescription);

        for (var v = SeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var appliedDescription = Assert.Contains(v, rowsByVersion);
            Assert.False(
                appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be an applied migration row, not a synthetic bootstrap row " +
                $"(description was: '{appliedDescription}')");
        }

        //Assert — table now at V7 shape with marker row still present.
        var columnsAfterRetry = await GetTableColumns();
        Assert.Contains("dataref", columnsAfterRetry); // V7 column
        Assert.Contains("partitionkey", columnsAfterRetry); // V4 column applied on retry
        Assert.Equal(1, await GetMarkerRowCount());
    }

    private async Task SeedMarkerRow()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO \"{_tableName}\" (messageid) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
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
        try
        {
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows[reader.GetInt32(0)] = reader.GetString(1);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // undefined_table — history table does not exist (rolled back before EnsureHistoryTableAsync committed).
        }
        return rows;
    }

    private async Task<long> GetMarkerRowCount()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM \"{_tableName}\" WHERE messageid = @MessageId";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
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
            catch (PostgresException) { /* history table may not exist */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private sealed class BrokenChainCatalog : IAmABoxMigrationCatalog
    {
        private readonly IReadOnlyList<IAmABoxMigration> _migrations;
        private readonly string _freshInstallDdl;

        public BrokenChainCatalog(IReadOnlyList<IAmABoxMigration> migrations, string freshInstallDdl)
        {
            _migrations = migrations;
            _freshInstallDdl = freshInstallDdl;
        }

        public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration) => _migrations;
        public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration) => _freshInstallDdl;
    }
}
