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

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class SqliteRunnerSqliteBusyContentionTests
{
    //Writer holds the SQLite reserved/exclusive lock long enough that any "no retry" runner must
    //fail with SQLITE_BUSY before the writer releases. 600ms is well above CI scheduler jitter
    //but short enough to keep the test cheap.
    private static readonly TimeSpan WriterHold = TimeSpan.FromMilliseconds(600);

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

    [Test]
    public async Task When_runner_contends_with_concurrent_writer_it_should_retry_sqlite_busy_with_backoff_and_succeed()
    {
        //Arrange — file-backed SQLite database in WAL mode so writers contend on a single
        //writer slot. A separate "writer" connection takes BEGIN IMMEDIATE and dirties the
        //database (issues a write so the reserved lock is upgraded to a real writer lock),
        //then releases after WriterHold. Concurrently, the runner must call MigrateAsync —
        //its own attempt to acquire the writer slot must wait, retry on SQLITE_BUSY, and
        //ultimately succeed once the writer commits.
        await EnsureWalModeAsync(_connectionString);

        //Use a 1-entry V1-only migration list so this test isolates the SQLITE_BUSY retry
        //concern. Multi-version V1..V7 migrations bring in IdempotencyCheckSql skip / TOCTOU
        //re-check / bootstrap branching — those are verified by Tasks 4.6 / 4.7 / 4.8 / 4.9
        //per spec line 684. Here we just want: "runner can wait for a writer to release".
        var freshState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var writer = new SqliteConnection(_connectionString);
        await writer.OpenAsync();
        var writerTransaction = (SqliteTransaction)await writer.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        //Force an immediate writer-lock acquisition by issuing a write inside the transaction.
        //SQLite's serializable isolation acquires the reserved lock lazily; doing a write
        //promotes it to a writer lock, which is what we need to make the runner contend.
        await ExecuteWriteAsync(writer, writerTransaction,
            "CREATE TABLE IF NOT EXISTS [__sqlite_contention_marker] ([id] INTEGER NOT NULL)");

        var releaseWriter = Task.Run(async () =>
        {
            await Task.Delay(WriterHold);
            await writerTransaction.CommitAsync();
            await writer.DisposeAsync();
        });

        //Act — runner kicks off concurrent with the writer hold and must observe SQLITE_BUSY,
        //back off, and retry until the writer releases (~600ms later) and it can acquire the
        //lock itself.
        var stopwatch = Stopwatch.StartNew();
        var migrateTask = _runner.MigrateAsync(
            _tableName,
            schemaName: null,
            BoxType.Outbox,
            freshState);

        var thrown = await TestExceptionRecorder.CaptureAsync(async () => await migrateTask);
        stopwatch.Stop();
        await releaseWriter;

        //Assert — the runner finished without surfacing SQLITE_BUSY (i.e. it retried internally
        //and succeeded), the V1 history row landed exactly once (no duplicates), and the wall
        //clock spans at least ~80% of WriterHold so we know the runner actually waited rather
        //than racing past the lock by accident. The slack accommodates CI scheduler jitter.
        await Assert.That(thrown).IsNull();
        await Assert.That(await GetHistoryRowCountAsync(_tableName, version: 1)).IsEqualTo(1);
        var minimumExpectedWait = WriterHold - TimeSpan.FromMilliseconds(120);
        await Assert.That(stopwatch.Elapsed >= minimumExpectedWait).IsTrue();
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

    private static async Task ExecuteWriteAsync(
        SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
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

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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
