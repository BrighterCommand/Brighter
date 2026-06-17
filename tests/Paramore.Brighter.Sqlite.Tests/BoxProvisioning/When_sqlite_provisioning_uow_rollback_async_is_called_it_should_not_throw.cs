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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Sqlite.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class ProvisioningUnitOfWorkRollbackTests : IAsyncLifetime
{
    // Per ADR 0058 §B.3: if CommitAsync throws, the runner enters its catch path and calls
    // RollbackAsync — even though the commit was already attempted. The underlying
    // SqliteTransaction may already be finalised by that point (committed-but-client-side-failed,
    // or zombied by a closed connection). RollbackAsync MUST therefore be best-effort: attempt
    // the rollback, swallow the "already finalised" failure, log a Warning, and return cleanly.
    // It MUST NOT throw — the runner's unwind cannot be allowed to abandon. This contract is
    // shared with MSSQL 5.1.c and Postgres 5.2.c; only the underlying-driver exception shape
    // changes (Microsoft.Data.Sqlite throws InvalidOperationException with message
    // "Transaction has completed; it is no longer usable." — same as MSSQL and Postgres).
    //
    // The post-failed-commit state is simulated by calling SqliteTransaction.Commit() directly
    // out-of-band: externally indistinguishable from a thrown CommitAsync — both leave the
    // SqliteTransaction in the completed state where Rollback() throws InvalidOperationException.
    // The 5.4.b RollbackAsync stub is `Task.CompletedTask` — it returns silently and emits no
    // log entry, so the Warning assertion fails until the impl actually calls through to the
    // underlying transaction AND catches the resulting exception with a Warning-level log entry.

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_uow_rollback_{Guid.NewGuid():N}.db");

    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public ProvisioningUnitOfWorkRollbackTests()
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
    public async Task When_sqlite_provisioning_uow_rollback_async_is_called_it_should_not_throw()
    {
        // Arrange
        var capturingLogger = new CapturingLogger();
        await using var uow = new SqliteProvisioningUnitOfWork(_connection, capturingLogger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var transaction = uow.Transaction!;
        transaction.Commit();   // Force the post-failed-commit finalised state

        // Act
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(CancellationToken.None));

        // Assert: best-effort rollback returns cleanly AND emits a Warning. A no-op stub
        // returns cleanly too, but emits no log entry — the Warning assertion is the
        // discriminator that pins the impl actually called through to the underlying
        // SqliteTransaction.RollbackAsync and caught the resulting InvalidOperationException.
        Assert.Null(thrown);
        Assert.Contains(capturingLogger.Entries, e => e.Level == LogLevel.Warning);
    }
}
