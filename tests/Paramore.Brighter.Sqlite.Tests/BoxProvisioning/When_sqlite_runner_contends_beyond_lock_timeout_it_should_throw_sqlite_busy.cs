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

public class SqliteRunnerLockTimeoutBoundsContentionTests
{
    // Sibling to SqliteRunnerSqliteBusyContentionTests. That test pins "the runner retries
    // SQLITE_BUSY transparently" using the default lockTimeout (30s) and a brief writer hold
    // (~600ms). It cannot distinguish "the runner honours lockTimeout" from "the driver's
    // default DefaultTimeout=30s is doing the waiting", because under default settings the two
    // are indistinguishable.
    //
    // This test pins the second property: the runner constructs the SqliteConnection with
    // DefaultTimeout derived from the caller-supplied lockTimeout, so a tight lockTimeout
    // surfaces SQLITE_BUSY quickly even when the writer would have released later. A failing
    // runner that ignored lockTimeout would inherit the driver's 30s default and silently
    // outlast WriterHold, masking the bug.
    //
    // Why this matters: per ADR 0058 §B.1 lockTimeout is the per-backend wait budget for
    // advisory-lock acquisition. SQLite has no advisory lock — BEGIN IMMEDIATE is itself the
    // acquisition — so honouring lockTimeout in SQLite means setting connection.DefaultTimeout
    // (Microsoft.Data.Sqlite drives sqlite3_busy_timeout from this; PRAGMA busy_timeout is
    // silently overwritten by the next command per the comment at
    // When_sqlite_provisioning_uow_begin_async_is_called_it_should_begin_immediate_as_writer_slot_lock.cs:53-60).
    // Writer holds the slot well past the runner's tight lockTimeout, so a runner that honours
    // lockTimeout MUST give up before the writer releases. 3s is well above CI scheduler jitter.
    private static readonly TimeSpan WriterHold = TimeSpan.FromSeconds(3);

    // 1 second is the smallest meaningful lockTimeout for SQLite (Microsoft.Data.Sqlite's
    // DefaultTimeout is an int seconds value; the runner floors anything sub-second at 1s).
    private static readonly TimeSpan TightLockTimeout = TimeSpan.FromSeconds(1);

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_lock_timeout_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly string _tableName = $"outbox_{Guid.NewGuid():N}";
    private readonly RelationalDatabaseConfiguration _config;
    private readonly SqliteBoxMigrationRunner _runner;

    public SqliteRunnerLockTimeoutBoundsContentionTests()
    {
        _connectionString = $"Data Source={_dbPath}";
        _config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        _runner = new SqliteBoxMigrationRunner(
            new SingleV1Catalog(_config), _config, TightLockTimeout);
    }

    [Test]
    public async Task When_writer_hold_outlasts_lock_timeout_it_should_throw_sqlite_busy()
    {
        // Arrange — file-backed SQLite in WAL mode; a separate writer connection takes the
        // writer slot and holds it for WriterHold (3s). The runner is constructed with a tight
        // 1s lockTimeout — well under WriterHold so a correct implementation MUST surface
        // SQLITE_BUSY before the writer releases.
        await EnsureWalModeAsync(_connectionString);

        var freshState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        var writer = new SqliteConnection(_connectionString);
        await writer.OpenAsync();
        var writerTransaction = (SqliteTransaction)await writer.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        await ExecuteWriteAsync(writer, writerTransaction,
            "CREATE TABLE IF NOT EXISTS [__sqlite_lock_timeout_marker] ([id] INTEGER NOT NULL)");

        var releaseWriter = Task.Run(async () =>
        {
            await Task.Delay(WriterHold);
            await writerTransaction.CommitAsync();
            await writer.DisposeAsync();
        });

        // Act — runner kicks off concurrent with the writer hold, retries SQLITE_BUSY for its
        // 1s lockTimeout budget, then surfaces SqliteException(SqliteErrorCode == 5) when the
        // budget expires (before WriterHold elapses).
        var stopwatch = Stopwatch.StartNew();
        var thrown = await TestExceptionRecorder.CaptureAsync(async () => await _runner.MigrateAsync(
            _tableName,
            schemaName: null,
            BoxType.Outbox,
            freshState));
        stopwatch.Stop();
        await releaseWriter;

        // Assert — SQLITE_BUSY surfaced as the bounded retry expired. SqliteErrorCode 5 is
        // SQLITE_BUSY; anything else means the runner waited the driver's 30s default and the
        // writer released first (the bug this test exists to prevent).
        var sqliteException = await Assert.That(thrown).IsTypeOf<SqliteException>();
        await Assert.That(sqliteException.SqliteErrorCode).IsEqualTo(5);

        // Assert — the runner gave up well before WriterHold. Slack (1.5s ceiling) accommodates
        // CI scheduler jitter while still proving the runner did not silently inherit 30s.
        var failFastCeiling = TimeSpan.FromMilliseconds(1500);
        await Assert.That(stopwatch.Elapsed < failFastCeiling).IsTrue();
    }

    private sealed class SingleV1Catalog : IAmABoxMigrationCatalog
    {
        private readonly IAmARelationalDatabaseConfiguration _config;
        public SingleV1Catalog(IAmARelationalDatabaseConfiguration config) => _config = config;

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
