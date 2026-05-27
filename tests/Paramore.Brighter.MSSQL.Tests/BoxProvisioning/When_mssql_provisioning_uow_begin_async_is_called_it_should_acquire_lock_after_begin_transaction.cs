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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_provisioning_uow_begin_async_is_called_it_should_acquire_lock_after_begin_transaction : IAsyncLifetime
{
    // Per ADR 0058 §B.1: MSSQL uses sp_getapplock with @LockOwner='Transaction'. The lock is
    // bound to the surrounding transaction's lifetime, so BeginAsync MUST call BeginTransaction
    // first and then pass that transaction into the advisory-lock primitive's AcquireAsync.
    // Reverse ordering would attempt to acquire a lock against a non-existent transaction.
    //
    // The ordering is proved with a spy IMsSqlAdvisoryLock that captures the SqlTransaction it
    // was called with. After BeginAsync returns, the test asserts reference-equality between
    // that captured transaction and the UoW's Transaction property. Only an implementation
    // that opens the transaction first and threads the same instance into AcquireAsync can
    // satisfy both Same-arguments — so this single assertion pins the §B.1 ordering contract.

    private readonly SqlConnection _connection = new(Configuration.DefaultConnectingString);
    private readonly FakeMsSqlAdvisoryLock _advisoryLock = new(throwOnAcquire: null);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Should_acquire_lock_with_the_transaction_opened_during_BeginAsync()
    {
        // Arrange
        await using var uow = new MsSqlProvisioningUnitOfWork(_connection, _advisoryLock, NullLogger.Instance);

        // Act
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Assert: BeginTransaction ran before AcquireAsync — the spy captured a non-null
        // transaction and it is the same instance now exposed by the UoW.
        Assert.NotNull(uow.Transaction);
        Assert.NotNull(_advisoryLock.CapturedTransaction);
        Assert.Same(uow.Transaction, _advisoryLock.CapturedTransaction);
    }
}
