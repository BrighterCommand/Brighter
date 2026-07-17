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

/// <summary>
/// Per ADR 0058 §B.1 the Postgres UoW acquires the session-scoped <c>pg_advisory_lock</c>
/// BEFORE opening the transaction (the lock outlives any tx on the connection, so it must
/// be acquired outside any in-flight tx). That ordering creates a partial-init window: if
/// <c>BeginTransactionAsync</c> throws AFTER <c>AcquireAsync</c> has succeeded, the lock is
/// held with no transaction wrapper. The §B.3 runner contract places <c>BeginAsync</c>
/// OUTSIDE the runner's try/catch (so <c>RollbackAsync</c> is not called when Begin fails
/// — pinned by the sibling base-runner test
/// <c>When_relational_box_migration_runner_base_begin_async_throws_it_should_skip_commit_and_rollback_and_still_dispose</c>),
/// which means the leak window can only be closed inside <c>BeginAsync</c> itself.
/// <para>
/// This test pins the cleanup contract: if <c>BeginTransactionAsync</c> throws after the
/// lock was acquired, <c>BeginAsync</c> must release the lock before propagating the
/// transaction exception unwrapped. The session-scope of the lock bounds the leak to
/// connection close in the worst case, but the cleanup at source restores atomic-Begin
/// semantics so callers can rely on "BeginAsync threw ⇒ no resources held".
/// </para>
/// </summary>
/// <remarks>
/// The test forces <c>BeginTransactionAsync</c> to throw by opening an outer transaction
/// on the connection before calling <c>BeginAsync</c>. Npgsql refuses nested transactions
/// with <c>InvalidOperationException</c> ("A transaction is already in progress;
/// nested/concurrent transactions aren't supported."), which is the same surface a real
/// driver/server failure would present at this seam.
/// </remarks>
public class PostgreSqlProvisioningUnitOfWorkBeginTransactionThrowsTests : IAsyncLifetime
{
    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(releaseResult: true);
    private NpgsqlTransaction? _blockingTransaction;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        // Open an outer tx so any subsequent BeginTransactionAsync on the same connection
        // throws InvalidOperationException — Npgsql does not support nested/concurrent tx.
        _blockingTransaction = await _connection.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_blockingTransaction is not null) await _blockingTransaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task When_postgres_provisioning_uow_begin_transaction_throws_after_acquire_it_should_release_the_lock()
    {
        const string lockKey = "test_lock_resource_begintx_failure";

        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, _advisoryLock, NullLogger.Instance);

        var thrown = await Record.ExceptionAsync(() => uow.BeginAsync(
            lockResource: lockKey,
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None));

        // Original BeginTransactionAsync exception propagates unwrapped (§B.3 contract).
        Assert.IsType<InvalidOperationException>(thrown);

        // AcquireAsync ran first per §B.1 ordering — pin it so the test fails clearly if
        // the ordering ever regresses to "BeginTransaction first".
        Assert.Equal(lockKey, _advisoryLock.AcquiredKey);

        // The cleanup contract: BeginAsync released the lock before propagating the
        // transaction exception. Without this fix, the session-scoped lock would leak to
        // connection close — bounded but messy.
        Assert.Equal(lockKey, _advisoryLock.ReleasedKey);
    }
}
