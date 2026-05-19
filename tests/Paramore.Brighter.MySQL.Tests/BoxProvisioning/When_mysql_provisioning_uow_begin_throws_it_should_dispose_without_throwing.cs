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

public class When_mysql_provisioning_uow_begin_throws_it_should_dispose_without_throwing : IAsyncLifetime
{
    // Per ADR 0058 §B.3: the runner declares the UoW with `await using`, so DisposeAsync runs
    // on every exit path — including when BeginAsync itself throws. MySQL-specific subtlety:
    // the UoW is transactionless (ADR 0057 §5a), so the only state BeginAsync touches is the
    // _lockResource field. Per F2 (PR #4039 reviewer item M2-3), _lockResource is assigned
    // only AFTER AcquireAsync returns successfully — so when AcquireAsync throws, _lockResource
    // remains null. DisposeAsync MUST tolerate that partial-init state without throwing AND
    // MUST NOT call ReleaseAsync — because issuing RELEASE_LOCK on a never-acquired lock would
    // return NULL (no lock by that name) and produce a misleading tri-state Warning suggesting
    // a Brighter defect when the real failure was lock acquisition.
    //
    // The test wires a FakeMySqlAdvisoryLock that throws TimeoutException on AcquireAsync. The
    // whole `await using` block is wrapped in Record.ExceptionAsync. Because `await using` is
    // sugar for try/finally and C# replaces (not suppresses) the original exception when the
    // finally throws, a clean DisposeAsync surfaces the original TimeoutException — and a
    // throwing DisposeAsync would surface a different type. So Assert.IsType<TimeoutException>
    // pins both halves of the contract:
    //   1. BeginAsync propagates the AcquireAsync failure unwrapped
    //   2. DisposeAsync ran cleanly (no exception type substitution)
    // Assert.Null(_advisoryLock.ReleasedKey) pins the third half — DisposeAsync did not
    // erroneously call ReleaseAsync on a never-acquired lock. (Note: this test is a regression
    // guard — the current DisposeAsync impl returns `default` and never touches _advisoryLock,
    // so it satisfies the contract green-from-the-start. The test exists to prevent a future
    // "be helpful and unconditionally release on dispose" mutation from issuing RELEASE_LOCK
    // when the lock was never acquired.)

    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);
    private readonly FakeMySqlAdvisoryLock _advisoryLock = new(
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
            await using var uow = new MySqlProvisioningUnitOfWork(
                _connection, _advisoryLock, NullLogger.Instance);
            await uow.BeginAsync(
                lockResource: "test_lock_resource",
                lockTimeout: TimeSpan.FromSeconds(5),
                cancellationToken: CancellationToken.None);
        });

        // Assert — the surfaced exception is the BeginAsync TimeoutException; DisposeAsync did
        // not throw (otherwise its exception would have replaced the TimeoutException).
        Assert.IsType<TimeoutException>(thrown);
        // Assert — DisposeAsync did not call ReleaseAsync; the lock was never acquired (Acquire
        // threw before completing), so RELEASE_LOCK must not be issued — it would return NULL
        // and produce a misleading tri-state Warning.
        Assert.Null(_advisoryLock.ReleasedKey);
    }
}
