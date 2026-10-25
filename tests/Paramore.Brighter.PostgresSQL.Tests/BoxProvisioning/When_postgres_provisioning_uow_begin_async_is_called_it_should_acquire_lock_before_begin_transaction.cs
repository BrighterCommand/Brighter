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
/// BeginAsync acquires the session-scoped pg_advisory_lock before it begins the transaction (ADR 0058 §B.1). The fake lock
/// probes the connection when AcquireAsync is called, and finds no transaction active.
/// </summary>
public class PostgreSqlProvisioningUnitOfWorkBeginTests : IAsyncLifetime
{
    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock =
        new(releaseResult: true, senseTransactionStateAtAcquire: true);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
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

        // Assert
        Assert.False(_advisoryLock.TransactionWasActiveAtAcquireTime);
        Assert.NotNull(uow.Transaction);
    }
}
