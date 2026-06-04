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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class When_mysql_provisioning_uow_begin_async_is_called_it_should_acquire_get_lock_with_null_transaction : IAsyncLifetime
{
    // Per ADR 0057 §5a / ADR 0058 §B.1: MySQL is the transactionless backend in the relational
    // family — DDL statements implicitly commit the surrounding transaction, so wrapping a
    // multi-statement migration in BEGIN/COMMIT yields nothing useful. The MySQL UoW therefore
    // never opens a transaction; it only acquires the session-level GET_LOCK that serialises
    // concurrent runners. This is the load-bearing shape difference vs MSSQL/Postgres UoWs.
    //
    // The interface IAmAProvisioningUnitOfWork<TTransaction> is generic over the backend's
    // transaction subtype; MySqlProvisioningUnitOfWork instantiates TTransaction = MySqlTransaction
    // for symmetry but always returns null from the Transaction property — see XML-doc on
    // IAmAProvisioningUnitOfWork.Transaction (line 57-62) which spells out this contract.
    //
    // BeginAsync's contract is therefore:
    //   1. Invoke advisoryLock.AcquireAsync (the spy's AcquiredKey becomes non-null).
    //   2. Do NOT open a transaction — Transaction stays null.
    //
    // Pinning these two facts requires no further spy on the connection: any implementation
    // that called connection.BeginTransaction would be required to assign the result somewhere,
    // and the only valid sink that survives BeginAsync is the Transaction property (because
    // a stack-local would be lost). So `Assert.Null(uow.Transaction)` is the structural pin
    // for "did not open a transaction".

    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);
    private readonly FakeMySqlAdvisoryLock _advisoryLock = new(releaseResult: true);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Should_acquire_GET_LOCK_and_leave_Transaction_null()
    {
        // Arrange
        await using var uow = new MySqlProvisioningUnitOfWork(
            _connection, _advisoryLock, NullLogger.Instance);

        // Act
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Assert: GET_LOCK was acquired with the lock resource the runner asked for.
        Assert.Equal("test_lock_resource", _advisoryLock.AcquiredKey);
        // Assert: no transaction was opened — MySQL DDL auto-commits, so a wrapping tx serves
        // no purpose and would be misleading. Per ADR 0057 §5a / ADR 0058 §B.1.
        Assert.Null(uow.Transaction);
    }
}
