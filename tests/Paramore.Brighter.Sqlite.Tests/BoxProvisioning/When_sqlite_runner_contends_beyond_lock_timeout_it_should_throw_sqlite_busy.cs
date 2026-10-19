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
/// The runner derives the connection's DefaultTimeout from lockTimeout, so a tight lockTimeout surfaces
/// SQLITE_BUSY before a longer writer hold ends, rather than waiting out the driver's 30-second default.
/// </summary>
public class SqliteRunnerLockTimeoutBoundsContentionTests : IAsyncLifetime
{
    // Outlasts TightLockTimeout by more than CI scheduler jitter.
    private static readonly TimeSpan WriterHold = TimeSpan.FromSeconds(3);

    // The smallest lockTimeout SQLite honours: DefaultTimeout is in whole seconds.
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

    [Fact]
    public async Task When_writer_hold_outlasts_lock_timeout_it_should_throw_sqlite_busy()
    {
        // Arrange
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

        // Act
        var stopwatch = Stopwatch.StartNew();
        var thrown = await Record.ExceptionAsync(async () => await _runner.MigrateAsync(
            _tableName,
            schemaName: null,
            BoxType.Outbox,
            freshState));
        stopwatch.Stop();
        await releaseWriter;

        // Assert
        var sqliteException = Assert.IsType<SqliteException>(thrown);
        Assert.Equal(5, sqliteException.SqliteErrorCode);

        var failFastCeiling = TimeSpan.FromMilliseconds(1500);
        Assert.True(
            stopwatch.Elapsed < failFastCeiling,
            $"Runner returned in {stopwatch.Elapsed.TotalMilliseconds:F0}ms but lockTimeout " +
            $"was {TightLockTimeout.TotalMilliseconds:F0}ms — expected the runner to give up " +
            $"under {failFastCeiling.TotalMilliseconds:F0}ms, suggesting it ignored lockTimeout " +
            $"and inherited the driver's 30s default.");
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
