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

public class MsSqlProvisioningUnitOfWorkCommitTests : IAsyncLifetime
{
    // Per ADR 0058 §B.1: MSSQL uses sp_getapplock with @LockOwner='Transaction'. CommitAsync
    // therefore only needs to commit the underlying SqlTransaction — SQL Server releases the
    // application lock implicitly when the transaction completes. The "no explicit lock
    // release" contract is enforced structurally by IMsSqlAdvisoryLock exposing only
    // AcquireAsync (no Release method exists), so this test pins only the commit half of the
    // contract: after CommitAsync, the SqlTransaction is in the completed state.
    //
    // Re-invoking Commit() on a completed SqlTransaction throws InvalidOperationException
    // ("This SqlTransaction has completed; it is no longer usable."). A no-op CommitAsync
    // would leave the transaction active and the second Commit() would succeed silently — so
    // this single assertion fails for a no-op and passes only when CommitAsync actually
    // committed.

    private readonly SqlConnection _connection = new(Configuration.DefaultConnectingString);
    private readonly FakeMsSqlAdvisoryLock _advisoryLock = new(throwOnAcquire: null);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_mssql_provisioning_uow_commit_async_is_called_it_should_commit_transaction_without_explicit_lock_release()
    {
        // Arrange
        await using var uow = new MsSqlProvisioningUnitOfWork(_connection, _advisoryLock, NullLogger.Instance);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var transaction = uow.Transaction!;

        // Act
        await uow.CommitAsync(CancellationToken.None);

        // Assert: post-commit, re-issuing Commit on the same SqlTransaction throws because the
        // transaction is already completed. A no-op CommitAsync would leave the tx active and
        // this call would succeed silently.
        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }
}
