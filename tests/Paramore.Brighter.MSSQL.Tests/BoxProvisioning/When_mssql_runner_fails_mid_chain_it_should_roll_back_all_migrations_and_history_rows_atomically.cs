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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.Legacy;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// A migration failing mid-chain rolls back every migration and history row in the batch, leaving the
/// table and its rows as they were; a retry with the real chain then completes (ADR 0057 §5a).
/// </summary>
public class MsSqlRunnerMidChainFailureRollbackTests
{
    private const int SeedVersion = 3;
    private const int BrokenVersion = 6;
    private const string BrokenUpScript = "RAISERROR('forced failure for spec 0027 task 1.8a', 16, 1);";
    private const string MarkerMessageId = "marker-row-must-survive";
    private const string V3Column = "ContentType";
    private const string V4Column = "PartitionKey";
    private const string V5Column = "Source";
    private const string V7Column = "DataRef";

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_mssql_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        MsSqlOutboxLegacySeeder.SeedAtV(SeedVersion, _connectionString, _tableName);
        SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realCatalog = new MsSqlOutboxMigrationCatalog();
        var realMigrations = realCatalog.All(config);
        var brokenMigrations = BrokenMigrationFactory.WithBrokenVersion(
            realMigrations, BrokenVersion, BrokenUpScript);
        var brokenCatalog = new BrokenChainCatalog(brokenMigrations, realCatalog.FreshInstallDdl(config));

        var brokenRunner = new MsSqlBoxMigrationRunner(brokenCatalog, config, TimeSpan.FromSeconds(30));
        var staleHint = new BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: SeedVersion);

        //Act + Assert
        await Assert.That(() => brokenRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint)).ThrowsExactly<SqlException>();

        await Assert.That(GetHistoryRowsByVersion()).IsEmpty();

        var columnsAfterFailure = GetTableColumns();
        await Assert.That(columnsAfterFailure).Contains(V3Column);
        await Assert.That(columnsAfterFailure).DoesNotContain(V4Column);
        await Assert.That(columnsAfterFailure).DoesNotContain(V5Column);

        await Assert.That(GetMarkerRowCount()).IsEqualTo(1);

        //Act + Assert
        var realRunner = new MsSqlBoxMigrationRunner(realCatalog, config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            realRunner);
        await provisioner.ProvisionAsync();

        var rowsByVersion = GetHistoryRowsByVersion();
        await Assert.That(rowsByVersion.Count).IsEqualTo(ExpectedMigrationVersions.OutboxLatest - SeedVersion + 1);

        await Assert.That(rowsByVersion.ContainsKey(SeedVersion)).IsTrue();

        var syntheticDescription = rowsByVersion[SeedVersion];
        await Assert.That(syntheticDescription).StartsWith($"bootstrap: detected at V{SeedVersion}");

        for (var v = SeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            await Assert.That(rowsByVersion.ContainsKey(v)).IsTrue();
            var appliedDescription = rowsByVersion[v];
            await Assert.That(appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse();
        }

        var columnsAfterRetry = GetTableColumns();
        await Assert.That(columnsAfterRetry).Contains(V7Column);
        await Assert.That(columnsAfterRetry).Contains(V4Column);
        await Assert.That(GetMarkerRowCount()).IsEqualTo(1);
    }

    private void SeedMarkerRow()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO [{_tableName}] ([MessageId]) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        command.ExecuteNonQuery();
    }

    private HashSet<string> GetTableColumns()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'";
        command.Parameters.AddWithValue("@TableName", _tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private Dictionary<int, string> GetHistoryRowsByVersion()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NULL
    SELECT TOP 0 0 AS MigrationVersion, '' AS Description
ELSE
    SELECT [MigrationVersion], [Description] FROM [__BrighterMigrationHistory]
    WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo'
    ORDER BY [MigrationVersion]";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);

        var rows = new Dictionary<int, string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    private int GetMarkerRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{_tableName}] WHERE [MessageId] = @MessageId";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        return (int)command.ExecuteScalar()!;
    }

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            dropTable.ExecuteNonQuery();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NOT NULL
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
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
