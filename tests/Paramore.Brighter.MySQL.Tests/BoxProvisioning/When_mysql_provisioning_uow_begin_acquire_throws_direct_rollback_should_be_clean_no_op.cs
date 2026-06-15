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
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlProvisioningUnitOfWorkBeginAcquireThrowsTests : IAsyncLifetime
{
    // Companion to the partial-init DisposeAsync test
    // (MySqlProvisioningUnitOfWorkBeginThrowsTests): that one
    // exercises the `await using` dispose path; this one exercises a direct RollbackAsync call
    // after a failed BeginAsync. Both must be clean no-ops because the lock was never acquired
    // — issuing RELEASE_LOCK on a never-acquired lock would return NULL and produce a
    // misleading tri-state Warning suggesting a Brighter defect when the real failure was
    // lock acquisition (see ReleaseLockAndLogTriStateAsync, spec 0027 Item M / ADR 0057 §5b).
    //
    // Per ADR 0058 §B.3, the runner's contract is that Commit/Rollback are NOT called on a
    // failed BeginAsync — but the UoW must still tolerate a direct Rollback after Begin failure
    // because (a) RollbackAsync is disposal-style "MUST NOT throw", and (b) it is the natural
    // defensive cleanup path future contributors may reach for. The PostgreSqlProvisioningUnitOfWork
    // achieves this by clearing `_lockResource = null` in its BeginAsync catch; MySQL achieves
    // it by setting `_lockResource` only AFTER AcquireAsync succeeds (Phase F2 reviewer fix —
    // PR #4039 review item M2-3).
    //
    // The pre-fix MySQL UoW set `_lockResource = lockResource` BEFORE AcquireAsync, so when
    // Acquire threw, `_lockResource` was left non-null. A subsequent direct RollbackAsync would
    // then call ReleaseAsync on a never-acquired lock, get NULL back, and emit a misleading
    // Warning. This test RED-flags that pre-fix behaviour: the FakeMySqlAdvisoryLock is wired
    // with releaseResult=null so if Release IS called, it returns NULL and the UoW emits a
    // marker="NULL" Warning. After the F2 fix (swap the two assignment lines), Release is
    // never called and the Warning is never emitted.

    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);
    private readonly FakeMySqlAdvisoryLock _advisoryLock = new(
        releaseResult: null,
        throwOnAcquire: new TimeoutException("forced lock-acquisition failure for F2 reviewer test"));

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_mysql_provisioning_uow_begin_acquire_throws_direct_rollback_should_be_clean_no_op()
    {
        // Arrange — capturing logger so we can assert on Warning emissions.
        var logger = new CapturingLogger();
        var uow = new MySqlProvisioningUnitOfWork(_connection, _advisoryLock, logger);

        // Act — BeginAsync calls AcquireAsync which throws. Capture and discard the throw;
        // the contract under test is what happens NEXT when the caller (defensively) calls
        // RollbackAsync directly even though the runner contract says it would not.
        var beginThrown = await Record.ExceptionAsync(() => uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None));
        Assert.IsType<TimeoutException>(beginThrown);

        var rollbackThrown = await Record.ExceptionAsync(() =>
            uow.RollbackAsync(CancellationToken.None));

        // Assert — RollbackAsync did not throw (disposal-style semantics).
        Assert.Null(rollbackThrown);

        // Assert — RollbackAsync did NOT call ReleaseAsync on the never-acquired lock.
        // Release on a never-acquired lock returns NULL and emits a "Brighter defect"
        // Warning — both of which would mislead an operator trying to diagnose the
        // original acquisition failure.
        Assert.Null(_advisoryLock.ReleasedKey);

        // Assert — no Warning emitted. The original Acquire failure is the only event the
        // operator should be reasoning about; a release-side diagnostic would add noise.
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Empty(warnings);
    }
}
