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
/// If BeginTransactionAsync throws after AcquireAsync has taken the session-scoped lock, BeginAsync releases the lock and
/// rethrows the original exception unwrapped, so a BeginAsync that throws holds nothing (ADR 0058 §B.3). An outer transaction
/// on the connection makes BeginTransactionAsync throw, as Npgsql does not support nested transactions.
/// </summary>
public class PostgreSqlProvisioningUnitOfWorkBeginTransactionThrowsTests : IAsyncLifetime
{
    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(releaseResult: true);
    private NpgsqlTransaction? _blockingTransaction;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
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

        Assert.IsType<InvalidOperationException>(thrown);

        Assert.Equal(lockKey, _advisoryLock.AcquiredKey);

        Assert.Equal(lockKey, _advisoryLock.ReleasedKey);
    }
}
