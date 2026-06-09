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
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlProvisioningUnitOfWorkCommitTests : IAsyncLifetime
{
    // Per ADR 0058 §B.1: Postgres pg_advisory_lock is session-scoped — it is NOT released
    // implicitly when the surrounding transaction commits (unlike MSSQL's
    // @LockOwner='Transaction' sp_getapplock, where the lock is bound to the tx lifetime).
    // CommitAsync therefore has TWO obligations: commit the underlying transaction AND
    // explicitly call pg_advisory_unlock via IPostgreSqlAdvisoryLock.ReleaseAsync. Both halves
    // of the contract are pinned by this single test:
    //
    //   - The "tx was committed" half is pinned by capturing the NpgsqlTransaction reference
    //     after BeginAsync; after CommitAsync returns, re-issuing tx.Commit() must throw
    //     InvalidOperationException ("Cannot commit; this transaction has been completed..."
    //     in Npgsql parlance). A no-op CommitAsync would leave the tx active and the second
    //     Commit() would succeed silently.
    //
    //   - The "lock was released" half is pinned by the fake's ReleasedKey property, which
    //     records the lock key whenever ReleaseAsync is invoked. After CommitAsync, that
    //     property must equal the lock resource passed to BeginAsync. A CommitAsync that
    //     forgets to release the lock would leave ReleasedKey null.

    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(releaseResult: true);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_postgres_provisioning_uow_commit_async_is_called_it_should_commit_then_release_lock()
    {
        // Arrange
        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, _advisoryLock, NullLogger.Instance);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var capturedTx = uow.Transaction!;

        // Act
        await uow.CommitAsync(CancellationToken.None);

        // Assert: tx was committed — re-issuing Commit on the captured reference throws
        // "transaction has been completed". A no-op CommitAsync would leave the tx active and
        // this second Commit() would succeed silently.
        Assert.Throws<InvalidOperationException>(() => capturedTx.Commit());
        // Assert: pg_advisory_unlock was called explicitly with the same lock resource.
        Assert.Equal("test_lock_resource", _advisoryLock.ReleasedKey);
    }
}
