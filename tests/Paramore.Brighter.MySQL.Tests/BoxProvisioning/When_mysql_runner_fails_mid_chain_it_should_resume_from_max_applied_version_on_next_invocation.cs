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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.Legacy;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlRunnerMidChainFailureResumeTests : IAsyncLifetime
{
    private const int SeedVersion = 3;
    private const int BrokenVersion = 6;
    private const string BrokenUpScript =
        "SELECT 1 FROM non_existent_table_for_spec_0027_task_3_4;";
    private const string MarkerMessageId = "marker-row-must-survive";

    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_mysql_runner_fails_mid_chain_it_should_resume_from_max_applied_version_on_next_invocation()
    {
        //Arrange — seed an outbox at V3 (no history) plus a marker row to prove preservation.
        MySqlOutboxLegacySeeder.SeedAtV(SeedVersion, _connectionString, _tableName);
        await SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realCatalog = new MySqlOutboxMigrationCatalog();
        var realMigrations = realCatalog.All(config);
        var brokenMigrations = BrokenMigrationFactory.WithBrokenVersion(
            realMigrations, BrokenVersion, BrokenUpScript);
        var brokenCatalog = new BrokenChainCatalog(brokenMigrations, realCatalog.FreshInstallDdl(config));

        var brokenRunner = new MySqlBoxMigrationRunner(brokenCatalog, config, TimeSpan.FromSeconds(30));
        var staleHint = new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: SeedVersion);

        //Act + Assert (1) — broken V6 in chain: runner throws, but per ADR §5a MySQL implicit-DDL
        //commit means the V4/V5 ALTERs and history INSERTs already committed before V6 failed.
        await Assert.ThrowsAsync<MySqlException>(() => brokenRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint));

        //Assert — exactly 3 history rows survive: synthetic V3 (bootstrap) + applied V4 + applied V5.
        //The runner's three-path branching emits ONE synthetic row at the detected version (V3),
        //not one row per version 1..3 — so V1/V2 history rows must be absent.
        var rowsByVersion = await GetHistoryRowsByVersion();
        Assert.Equal(3, rowsByVersion.Count);
        Assert.True(rowsByVersion.ContainsKey(SeedVersion));
        Assert.True(rowsByVersion.ContainsKey(SeedVersion + 1));
        Assert.True(rowsByVersion.ContainsKey(SeedVersion + 2));
        Assert.StartsWith($"bootstrap: detected at V{SeedVersion}", rowsByVersion[SeedVersion]);
        Assert.False(
            rowsByVersion[SeedVersion + 1].StartsWith("bootstrap:", StringComparison.Ordinal),
            "V4 should be an applied migration row, not a synthetic bootstrap row");
        Assert.False(
            rowsByVersion[SeedVersion + 2].StartsWith("bootstrap:", StringComparison.Ordinal),
            "V5 should be an applied migration row, not a synthetic bootstrap row");

        //Assert — table shape reflects V5 (CloudEvents Source column committed) but not V6.
        var columnsAfterFailure = await GetTableColumns();
        Assert.Contains("PartitionKey", columnsAfterFailure); // V4 ALTER committed
        Assert.Contains("Source", columnsAfterFailure);       // V5 ALTER committed
        Assert.DoesNotContain("WorkflowId", columnsAfterFailure); // V6 never applied (script threw before ALTER)

        //Assert — seeded marker row preserved.
        Assert.Equal(1, await GetMarkerRowCount());

        //Act + Assert (2) — retry with the real migration list via the provisioner.
        var realRunner = new MySqlBoxMigrationRunner(realCatalog, config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            realRunner);
        await provisioner.ProvisionAsync();

        //Assert — V6 + V7 now applied; total exactly 5 history rows (V3 synthetic + V4..V7 applied).
        var rowsAfterRetry = await GetHistoryRowsByVersion();
        Assert.Equal(5, rowsAfterRetry.Count);

        for (var v = SeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            Assert.True(rowsAfterRetry.ContainsKey(v), $"Expected applied row for V{v}");
            Assert.False(
                rowsAfterRetry[v].StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be an applied migration row on retry");
        }

        //Assert — table now at V7 shape with marker still present.
        var columnsAfterRetry = await GetTableColumns();
        Assert.Contains("WorkflowId", columnsAfterRetry); // V6 applied on retry
        Assert.Contains("DataRef", columnsAfterRetry);    // V7 applied on retry
        Assert.Equal(1, await GetMarkerRowCount());
    }

    private async Task SeedMarkerRow()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO `{_tableName}` (`MessageId`, `Topic`, `MessageType`, `Timestamp`, `HeaderBag`, `Body`) " +
            "VALUES (@MessageId, 'topic', 'MT_EVENT', NOW(3), '{}', '{}')";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HashSet<string>> GetTableColumns()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM information_schema.columns
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName";
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
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT `MigrationVersion`, `Description` FROM `__BrighterMigrationHistory`
WHERE `BoxTableName` = @BoxTableName
ORDER BY `MigrationVersion`";
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
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            // history table absent — no rows to return
        }
        return rows;
    }

    private async Task<long> GetMarkerRowCount()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM `{_tableName}` WHERE `MessageId` = @MessageId";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS `{_tableName}`";
            await dropTable.ExecuteNonQueryAsync();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText =
                "DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (MySqlException) { /* history table may not exist */ }
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
