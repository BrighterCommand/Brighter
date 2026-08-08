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

#nullable enable

using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class ProvisioningUnitOfWorkBeginTests : IAsyncLifetime
{
    // Per ADR 0057 §4 / ADR 0058 §B.1: SQLite has no advisory-lock primitive — the writer slot
    // acquired by BEGIN IMMEDIATE is itself the lock. The SQLite UoW is therefore the
    // shape-different relational backend that takes only (SqliteConnection, ILogger) — no
    // IAmA*AdvisoryLock parameter — because there is nothing to acquire separately.
    //
    // BeginAsync's contract is therefore a pair of facts:
    //   1. Issue BEGIN IMMEDIATE on the connection so the database-wide RESERVED writer slot is
    //      held — pinned by observing that a sibling connection's BEGIN IMMEDIATE fails fast
    //      with SQLITE_BUSY (errno 5).
    //   2. Open a transaction — the same handle Transaction exposes — that the runner threads
    //      into command.Transaction for the migration DDL. Pinned by Assert.NotNull(uow.Transaction).
    //
    // The sibling probe is portable (it works against any Microsoft.Data.Sqlite driver version)
    // and avoids depending on the driver exposing a Trace / StatementInterceptor hook. The
    // sibling caps its internal busy_timeout to 1 second (DefaultTimeout=1) so the SQLITE_BUSY
    // surfaces in ~1 second rather than the driver's default 30s. PRAGMA busy_timeout=0 alone
    // doesn't help because Microsoft.Data.Sqlite's BeginTransaction creates its own internal
    // command whose CommandTimeout (= connection.DefaultTimeout) re-applies sqlite3_busy_timeout
    // before each call, overwriting the PRAGMA. DefaultTimeout=0 is interpreted by the driver
    // as "no timeout / wait forever" rather than "fail immediately", so the smallest positive
    // value is used.

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_uow_begin_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public ProvisioningUnitOfWorkBeginTests()
    {
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
    }

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) { File.Delete(path); } } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task When_sqlite_provisioning_uow_begin_async_is_called_it_should_begin_immediate_as_writer_slot_lock()
    {
        // Arrange — SqliteProvisioningUnitOfWork ctor takes only (SqliteConnection, ILogger).
        // The absence of an advisory-lock parameter is part of the contract and is locked in by
        // this construction expression — any future ctor overload that adds an IAmAdvisoryLock
        // parameter would break this test (it would no longer compile against the two-arg form).
        await using var uow = new SqliteProvisioningUnitOfWork(_connection, NullLogger.Instance);

        // Act
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Assert — Transaction property non-null after BeginAsync. BEGIN IMMEDIATE opens a
        // transaction atomically with acquiring the writer slot; the runner needs that handle
        // to thread into command.Transaction for the migration DDL.
        Assert.NotNull(uow.Transaction);

        // Assert — a sibling connection's BEGIN IMMEDIATE fails fast with SQLITE_BUSY (errno 5)
        // because the UoW holds the database-wide RESERVED writer slot. DefaultTimeout=1 caps
        // the wait at ~1 second so the test returns quickly; without this Microsoft.Data.Sqlite's
        // default 30s timeout would re-apply sqlite3_busy_timeout on the internal BEGIN command
        // and mask the busy state for that long.
        await using var sibling = new SqliteConnection(_connectionString);
        sibling.DefaultTimeout = 1;
        await sibling.OpenAsync();

        var siblingException = Record.Exception(
            () => sibling.BeginTransaction(IsolationLevel.Serializable, deferred: false));
        var sqliteException = Assert.IsType<SqliteException>(siblingException);
        Assert.Equal(5, sqliteException.SqliteErrorCode);
    }
}
