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

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// A migration that fails mid-chain rolls back the whole chain, DDL and history rows, in one transaction (ADR 0057 §5a), so a
/// retry with the real chain bootstraps from the seeded version and existing rows survive.
/// </summary>
public class PostgreSqlRunnerMidChainFailureRollbackTests
{
    private const int SeedVersion = 3;
    private const int BrokenVersion = 6;
    private const string BrokenUpScript =
        "DO $$ BEGIN RAISE EXCEPTION 'forced failure for spec 0027 task 2.7a'; END $$;";
    private const string MarkerMessageId = "marker-row-must-survive";
    private const string ColumnAddedInV3 = "contenttype";
    private const string ColumnAddedInV4 = "partitionkey";
    private const string ColumnAddedInV5 = "source";
    private const string ColumnAddedInV7 = "dataref";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_postgres_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically()
    {
        //Arrange
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

        //Act + Assert
        await Assert.That(() => brokenRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint)).ThrowsExactly<PostgresException>();

        await Assert.That(await GetHistoryRowsByVersion()).IsEmpty();

        var columnsAfterFailure = await GetTableColumns();
        await Assert.That(columnsAfterFailure).Contains(ColumnAddedInV3);
        await Assert.That(columnsAfterFailure).DoesNotContain(ColumnAddedInV4);
        await Assert.That(columnsAfterFailure).DoesNotContain(ColumnAddedInV5);

        await Assert.That(await GetMarkerRowCount()).IsEqualTo(1);

        //Act
        var realRunner = new PostgreSqlBoxMigrationRunner(realCatalog, config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            realRunner);
        await provisioner.ProvisionAsync();

        //Assert
        var rowsByVersion = await GetHistoryRowsByVersion();
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

        var columnsAfterRetry = await GetTableColumns();
        await Assert.That(columnsAfterRetry).Contains(ColumnAddedInV7);
        await Assert.That(columnsAfterRetry).Contains(ColumnAddedInV4);
        await Assert.That(await GetMarkerRowCount()).IsEqualTo(1);
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
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
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

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

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
