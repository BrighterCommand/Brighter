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

public class When_postgres_provisioning_uow_begin_throws_it_should_dispose_without_throwing : IAsyncLifetime
{
    // Per ADR 0058 §B.3: the runner declares the UoW with `await using`, so DisposeAsync runs
    // on every exit path — including when BeginAsync itself throws. Postgres-specific subtlety:
    // the §B.1 ordering puts AcquireAsync BEFORE BeginTransactionAsync (lock is session-scoped,
    // so it must be acquired outside any tx), which means an AcquireAsync failure leaves the
    // UoW with `_transaction == null` — a different partial-init state from MSSQL, where Begin
    // opens the tx first. DisposeAsync MUST tolerate that null-transaction state without
    // throwing, AND it MUST NOT call ReleaseAsync — the lock was never acquired (the throw
    // simulates a timeout or driver failure during pg_advisory_lock), so calling
    // pg_advisory_unlock would either be a no-op or, on a real connection, return false and
    // generate a misleading WARNING in Postgres logs. The only correct behaviour is "dispose
    // the (null) transaction handle, do not touch the lock".
    //
    // The test wires a FakePostgreSqlAdvisoryLock that throws TimeoutException on AcquireAsync.
    // The whole `await using` block is wrapped in Record.ExceptionAsync. Because `await using`
    // is sugar for try/finally and C# replaces (not suppresses) the original exception when
    // the finally throws, a clean DisposeAsync surfaces the original TimeoutException — and a
    // throwing DisposeAsync would surface a different type. So Assert.IsType<TimeoutException>
    // pins both halves of the contract:
    //   1. BeginAsync propagates the AcquireAsync failure unwrapped
    //   2. DisposeAsync ran cleanly (no exception type substitution)
    // Assert.Null(_advisoryLock.ReleasedKey) pins the third half — DisposeAsync did not
    // erroneously call ReleaseAsync on a never-acquired lock. (Note: this test is a regression
    // guard — the current DisposeAsync impl returns `default` when `_transaction is null` and
    // never calls ReleaseAsync, so it satisfies the contract green-from-the-start. The test
    // exists to prevent a future "be helpful and unconditionally release on dispose" mutation
    // from reintroducing the no-release-on-failed-acquire bug.)

    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(
        releaseResult: true,
        throwOnAcquire: new TimeoutException("forced lock-acquisition failure for spec 0028 §B.3 test"));

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Should_propagate_BeginAsync_exception_and_dispose_cleanly_without_releasing_lock()
    {
        // Act — capture whatever exception ultimately surfaces from the `await using` scope
        var thrown = await Record.ExceptionAsync(async () =>
        {
            await using var uow = new PostgreSqlProvisioningUnitOfWork(
                _connection, _advisoryLock, NullLogger.Instance);
            await uow.BeginAsync(
                lockResource: "test_lock_resource",
                lockTimeout: TimeSpan.FromSeconds(5),
                cancellationToken: CancellationToken.None);
        });

        // Assert — the surfaced exception is the BeginAsync TimeoutException; DisposeAsync did
        // not throw (otherwise its exception would have replaced the TimeoutException).
        Assert.IsType<TimeoutException>(thrown);
        // Assert — DisposeAsync did not call ReleaseAsync; the lock was never acquired, so
        // pg_advisory_unlock must not be issued.
        Assert.Null(_advisoryLock.ReleasedKey);
    }
}
