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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

/// <summary>
/// The runner retries SQLITE_BUSY with backoff while another connection holds the writer slot, and
/// succeeds once it is released.
/// </summary>
public class SqliteRunnerSqliteBusyContentionTests : IAsyncLifetime
{
    // Long enough that a runner which does not retry SQLITE_BUSY fails before the writer releases.
    private static readonly TimeSpan WriterHold = TimeSpan.FromMilliseconds(600);

    private static readonly TimeSpan SchedulerJitterSlack = TimeSpan.FromMilliseconds(120);

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_contention_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly string _tableName = $"outbox_{Guid.NewGuid():N}";
    private readonly RelationalDatabaseConfiguration _config;
    private readonly SqliteBoxMigrationRunner _runner;

    public SqliteRunnerSqliteBusyContentionTests()
    {
        _connectionString = $"Data Source={_dbPath}";
        _config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        _runner = new SqliteBoxMigrationRunner(new SingleV1Catalog(), _config);
    }

    [Fact]
    public async Task When_runner_contends_with_concurrent_writer_it_should_retry_sqlite_busy_with_backoff_and_succeed()
    {
        //Arrange
        await EnsureWalModeAsync(_connectionString);

        var freshState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var writer = new SqliteConnection(_connectionString);
        await writer.OpenAsync();
        var writerTransaction = (SqliteTransaction)await writer.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        await TakeWriterSlotAsync(writer, writerTransaction);

        var releaseWriter = Task.Run(async () =>
        {
            await Task.Delay(WriterHold);
            await writerTransaction.CommitAsync();
            await writer.DisposeAsync();
        });

        //Act
        var stopwatch = Stopwatch.StartNew();
        var migrateTask = _runner.MigrateAsync(
            _tableName,
            schemaName: null,
            BoxType.Outbox,
            freshState);

        var thrown = await Record.ExceptionAsync(async () => await migrateTask);
        stopwatch.Stop();
        await releaseWriter;

        //Assert
        Assert.Null(thrown);
        Assert.Equal(1, await GetHistoryRowCountAsync(_tableName, version: 1));
        var minimumExpectedWait = WriterHold - SchedulerJitterSlack;
        Assert.True(
            stopwatch.Elapsed >= minimumExpectedWait,
            $"Runner returned in {stopwatch.Elapsed.TotalMilliseconds:F0}ms but writer held the " +
            $"lock for ~{WriterHold.TotalMilliseconds:F0}ms — expected the runner to wait at " +
            $"least {minimumExpectedWait.TotalMilliseconds:F0}ms while retrying SQLITE_BUSY.");
    }

    private sealed class SingleV1Catalog : IAmABoxMigrationCatalog
    {
        public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration) =>
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, configuration.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
                })
        ];

        public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration) =>
            SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, configuration.BinaryMessagePayload);
    }

    private static async Task EnsureWalModeAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync();
    }

    // Serializable takes SQLite's reserved lock lazily; a write takes it now.
    private static async Task TakeWriterSlotAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "CREATE TABLE IF NOT EXISTS [__sqlite_contention_marker] ([id] INTEGER NOT NULL)";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> GetHistoryRowCountAsync(string boxTableName, int version)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [MigrationVersion] = @Version";
        command.Parameters.AddWithValue("@BoxTableName", boxTableName);
        command.Parameters.AddWithValue("@Version", version);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
        return Task.CompletedTask;

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) { File.Delete(path); } } catch { /* best-effort */ }
        }
    }
}
