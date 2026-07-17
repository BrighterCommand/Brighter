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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class ProvisioningUnitOfWorkCommitTests
{
    // Per ADR 0058 §B.1: SQLite's BEGIN IMMEDIATE transaction IS the lock — there is no
    // separate advisory-lock primitive to release. CommitAsync therefore only needs to commit
    // the underlying SqliteTransaction; SQLite releases the database-wide RESERVED writer slot
    // implicitly when the transaction completes. This is structurally analogous to the MSSQL
    // contract where sp_getapplock with @LockOwner='Transaction' is auto-released on tx
    // completion (see When_mssql_provisioning_uow_commit_async_…). The SQLite UoW has no
    // IAmA*AdvisoryLock collaborator, so this test pins only the commit half — proving the
    // tx was committed is proof the writer slot was released, because in SQLite they are the
    // same thing.
    //
    // Re-invoking Commit() on a completed SqliteTransaction throws InvalidOperationException
    // ("Transaction has completed; it is no longer usable."). A no-op CommitAsync (the 5.4.a
    // stub returned Task.CompletedTask) would leave the transaction active and the second
    // Commit() would succeed silently — so this single assertion fails for the stub and passes
    // only when CommitAsync actually committed.
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_uow_commit_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public ProvisioningUnitOfWorkCommitTests()
    {
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
    }

    [Before(Test)]
    public async Task InitializeAsync() => await _connection.OpenAsync();

    [After(Test)]
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

    [Test]
    public async Task When_sqlite_provisioning_uow_commit_async_is_called_it_should_commit_releasing_writer_slot()
    {
        // Arrange — SqliteProvisioningUnitOfWork ctor takes only (SqliteConnection, ILogger).
        await using var uow = new SqliteProvisioningUnitOfWork(_connection, NullLogger.Instance);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var transaction = uow.Transaction!;

        // Act
        await uow.CommitAsync(CancellationToken.None);

        // Assert: post-commit, re-issuing Commit on the same SqliteTransaction throws because
        // the transaction is already completed. A no-op CommitAsync would leave the tx active
        // and this call would succeed silently — committing the BEGIN IMMEDIATE transaction
        // is what releases the writer slot in SQLite.
        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }
}