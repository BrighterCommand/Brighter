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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// <c>RollbackAsync</c> on a transaction that has already completed — the state a thrown
/// <c>CommitAsync</c> can leave — logs a Warning and returns instead of throwing (ADR 0058 §B.3).
/// </summary>
public class MsSqlProvisioningUnitOfWorkRollbackAfterCommitThrewTests : IAsyncLifetime
{
    private readonly SqlConnection _connection = new(Configuration.DefaultConnectingString);
    private readonly FakeMsSqlAdvisoryLock _advisoryLock = new(throwOnAcquire: null);

    public async Task InitializeAsync()
    {
        await Configuration.EnsureDatabaseExistsAsync(_connection.ConnectionString);
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_mssql_provisioning_uow_rollback_async_is_called_after_commit_threw_it_should_not_throw()
    {
        // Arrange
        var capturingLogger = new CapturingLogger();
        await using var uow = new MsSqlProvisioningUnitOfWork(_connection, _advisoryLock, capturingLogger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);
        var transaction = uow.Transaction!;
        transaction.Commit();   // Force the post-failed-commit finalised state

        // Act
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(CancellationToken.None));

        // Assert
        Assert.Null(thrown);
        Assert.Contains(capturingLogger.Entries, e => e.Level == LogLevel.Warning);
    }
}
