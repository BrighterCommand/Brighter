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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlProvisioningUnitOfWorkRollbackTests : IAsyncLifetime
{
    // Per ADR 0058 §B.3: if CommitAsync throws, the runner enters its catch path and calls
    // RollbackAsync — even though the commit was already attempted. By that point the
    // underlying NpgsqlTransaction may already be finalised (committed-but-client-side-failed,
    // or zombied by a broken connection); calling _transaction.RollbackAsync() on a finalised
    // tx throws InvalidOperationException ("Cannot rollback; this transaction has been
    // completed..."). RollbackAsync MUST be best-effort: swallow the failure, log a Warning,
    // and STILL release the advisory lock — disposal-style semantics. It MUST NOT throw.
    //
    // Postgres differs from MSSQL: lock release is EXPLICIT (pg_advisory_unlock) since
    // pg_advisory_lock is session-scoped, NOT auto-released on tx completion. So this single
    // test pins THREE things at once:
    //
    //   - RollbackAsync does not throw despite the finalised tx
    //   - A Warning entry is logged about the rollback being skipped
    //   - The advisory lock is STILL released (best-effort means BOTH halves are attempted)
    //
    // The post-failed-commit state is simulated by calling NpgsqlTransaction.Commit() out-of-band
    // — externally indistinguishable from a thrown CommitAsync, since both leave the tx in the
    // "completed" state where the next Rollback() throws InvalidOperationException.

    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(releaseResult: true);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_postgres_provisioning_uow_rollback_async_is_called_it_should_release_lock_without_throwing()
    {
        // Arrange
        var capturingLogger = new CapturingLogger();
        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, _advisoryLock, capturingLogger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var transaction = uow.Transaction!;
        transaction.Commit();   // Force the post-failed-commit finalised state

        // Act
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(CancellationToken.None));

        // Assert: best-effort rollback returns cleanly...
        Assert.Null(thrown);
        // ...emits a Warning naming the rollback failure...
        Assert.Contains(capturingLogger.Entries, e => e.Level == LogLevel.Warning);
        // ...and STILL releases the advisory lock (both halves of the contract are attempted).
        Assert.Equal("test_lock_resource", _advisoryLock.ReleasedKey);
    }
}
