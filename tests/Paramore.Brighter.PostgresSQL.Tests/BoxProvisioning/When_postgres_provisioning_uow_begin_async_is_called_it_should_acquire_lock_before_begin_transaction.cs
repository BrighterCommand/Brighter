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

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlProvisioningUnitOfWorkBeginTests
{
    // Per ADR 0058 §B.1: Postgres uses pg_advisory_lock, which is session-scoped — the lock
    // outlives any transaction on the same connection and is released either by an explicit
    // pg_advisory_unlock or when the connection closes. BeginAsync MUST therefore acquire the
    // advisory lock BEFORE beginning the transaction (the reverse of the MSSQL ordering, which
    // uses sp_getapplock with @LockOwner='Transaction' and so requires BeginTransaction first).
    //
    // Postgres differs from MSSQL: IPostgreSqlAdvisoryLock.AcquireAsync only takes the connection
    // (no transaction handle is threaded through), so the MSSQL "captured-tx Same-instance" trick
    // is not available. Instead the spy probes the connection's transaction state at the moment
    // AcquireAsync is invoked — it attempts NpgsqlConnection.BeginTransactionAsync and watches
    // for the InvalidOperationException Npgsql throws when a transaction is already active.
    // Probe succeeds → no tx yet → AcquireAsync ran before BeginTransactionAsync.
    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock =
        new(releaseResult: true, senseTransactionStateAtAcquire: true);

    [Before(Test)]
    public async Task InitializeAsync() => await _connection.OpenAsync();

    [After(Test)]
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Test]
    public async Task When_postgres_provisioning_uow_begin_async_is_called_it_should_acquire_lock_before_begin_transaction()
    {
        // Arrange
        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, _advisoryLock, NullLogger.Instance);

        // Act
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Assert: at the moment AcquireAsync was called, no transaction was yet active on the
        // connection — proves AcquireAsync ran before BeginTransactionAsync. (Stays null if
        // AcquireAsync was never called, in which case Assert.False(null) also fails.)
        await Assert.That(_advisoryLock.TransactionWasActiveAtAcquireTime).IsFalse();
        // Assert: BeginTransactionAsync was also called — the UoW now exposes a non-null tx.
        await Assert.That(uow.Transaction).IsNotNull();
    }
}