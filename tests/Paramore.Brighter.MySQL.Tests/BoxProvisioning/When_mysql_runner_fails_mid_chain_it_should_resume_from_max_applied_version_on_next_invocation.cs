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

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// A MySQL chain that fails at V6 keeps the V4/V5 migrations and history rows it already committed
/// (DDL commits implicitly, ADR 0057 §5a) and the next run resumes from <c>MAX(V)</c>.
/// </summary>
public class MySqlRunnerMidChainFailureResumeTests
{
    private const int SeedVersion = 3;
    private const int BrokenVersion = 6;
    private const string BrokenUpScript =
        "SELECT 1 FROM non_existent_table_for_spec_0027_task_3_4;";
    private const string MarkerMessageId = "marker-row-must-survive";
    private const string V4Column = "PartitionKey";
    private const string V5Column = "Source";
    private const string V6Column = "WorkflowId";
    private const string V7Column = "DataRef";
    private const string V8Column = "CausationId";

    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_mysql_runner_fails_mid_chain_it_should_resume_from_max_applied_version_on_next_invocation()
    {
        //Arrange
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

        //Act + Assert
        await Assert.That(() => brokenRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint)).ThrowsExactly<MySqlException>();

        var rowsByVersion = await GetHistoryRowsByVersion();
        await Assert.That(rowsByVersion.Count).IsEqualTo(3);
        await Assert.That(rowsByVersion.ContainsKey(SeedVersion)).IsTrue();
        await Assert.That(rowsByVersion.ContainsKey(SeedVersion + 1)).IsTrue();
        await Assert.That(rowsByVersion.ContainsKey(SeedVersion + 2)).IsTrue();
        await Assert.That(rowsByVersion[SeedVersion]).StartsWith($"bootstrap: detected at V{SeedVersion}");
        await Assert.That(rowsByVersion[SeedVersion + 1].StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse().Because("V4 should be an applied migration row, not a synthetic bootstrap row");
        await Assert.That(rowsByVersion[SeedVersion + 2].StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse().Because("V5 should be an applied migration row, not a synthetic bootstrap row");

        var columnsAfterFailure = await GetTableColumns();
        await Assert.That(columnsAfterFailure).Contains(V4Column);
        await Assert.That(columnsAfterFailure).Contains(V5Column);
        await Assert.That(columnsAfterFailure).DoesNotContain(V6Column);

        await Assert.That(await GetMarkerRowCount()).IsEqualTo(1);

        //Act
        var realRunner = new MySqlBoxMigrationRunner(realCatalog, config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            realRunner);
        await provisioner.ProvisionAsync();

        //Assert
        var rowsAfterRetry = await GetHistoryRowsByVersion();
        await Assert.That(rowsAfterRetry.Count).IsEqualTo(6);

        for (var v = SeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            await Assert.That(rowsAfterRetry.ContainsKey(v)).IsTrue().Because($"Expected applied row for V{v}");
            await Assert.That(rowsAfterRetry[v].StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse().Because($"V{v} should be an applied migration row on retry");
        }

        var columnsAfterRetry = await GetTableColumns();
        await Assert.That(columnsAfterRetry).Contains(V6Column);
        await Assert.That(columnsAfterRetry).Contains(V7Column);
        await Assert.That(columnsAfterRetry).Contains(V8Column);
        await Assert.That(await GetMarkerRowCount()).IsEqualTo(1);
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

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

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
