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
/// Companion to <c>MsSqlProvisioningUnitOfWorkRollbackAfterCommitThrewTests</c>:
/// that test pins the post-finalised-commit path (<see cref="InvalidOperationException"/>);
/// this test pins the broader contract — RollbackAsync MUST NOT throw FOR ANY exception
/// type. Sibling backends (Postgres
/// <c>When_postgres_provisioning_uow_rollback_release_throws_it_should_log_without_throwing</c>)
/// already catch <see cref="Exception"/> on the equivalent unwind seam; MSSQL caught only
/// <see cref="InvalidOperationException"/>, which let zombied-connection
/// <see cref="SqlException"/> / <see cref="ObjectDisposedException"/> — and cancellation —
/// escape and mask the runner's primary migration error.
/// </summary>
/// <remarks>
/// We force a non-<see cref="InvalidOperationException"/> by handing
/// <c>uow.RollbackAsync</c> a pre-cancelled token: Microsoft.Data.SqlClient's
/// <c>SqlTransaction.RollbackAsync</c> short-circuits on a cancelled token with
/// <see cref="OperationCanceledException"/>, which the old narrow catch did not handle.
/// This is the simplest deterministic surface; the same widened catch covers the
/// zombied-connection cases the reviewer cited.
/// </remarks>
public class MsSqlProvisioningUnitOfWorkRollbackNonInvalidOperationTests : IAsyncLifetime
{
    private readonly SqlConnection _connection = new(Configuration.DefaultConnectingString);
    private readonly FakeMsSqlAdvisoryLock _advisoryLock = new(throwOnAcquire: null);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_mssql_provisioning_uow_rollback_throws_non_invalid_operation_exception_it_should_log_without_throwing()
    {
        var capturingLogger = new CapturingLogger();
        await using var uow = new MsSqlProvisioningUnitOfWork(_connection, _advisoryLock, capturingLogger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource_non_ioe_rollback",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Pre-cancel the token handed to RollbackAsync. SqlTransaction.RollbackAsync honours
        // the token and throws OperationCanceledException — a representative non-IOE surface
        // covering the same contract gap as a zombied connection raising SqlException.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act — RollbackAsync MUST NOT throw even though the inner _transaction.RollbackAsync
        // raises OperationCanceledException. Disposal-style contract: runner's catch path
        // (catch { uow.RollbackAsync(...); throw; }) cannot have its primary exception masked.
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(cts.Token));

        Assert.Null(thrown);

        // Best-effort path emitted a Warning naming the lock resource and carrying the
        // original exception, matching the existing finalised-tx branch's diagnostic shape.
        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
    }
}
