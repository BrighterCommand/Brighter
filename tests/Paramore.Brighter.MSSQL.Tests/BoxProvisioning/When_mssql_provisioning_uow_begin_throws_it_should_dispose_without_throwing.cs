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

public class MsSqlProvisioningUnitOfWorkBeginThrowsTests : IAsyncLifetime
{
    // Per ADR 0058 §B.3: the runner declares the UoW with `await using`, so DisposeAsync runs
    // on every exit path — including when BeginAsync itself throws. In that case the UoW may
    // be in a partial-init state (BeginTransaction succeeded, AcquireAsync threw), and
    // DisposeAsync MUST tolerate that state without throwing — otherwise the runner's catch
    // path sees the disposal exception instead of the underlying lock-acquisition failure and
    // the operator loses the actionable diagnostic.
    //
    // The test wires a FakeMsSqlAdvisoryLock that throws TimeoutException on AcquireAsync, so
    // BeginAsync opens the transaction successfully and then propagates the lock-acquisition
    // exception. The whole `await using` block is wrapped in Record.ExceptionAsync. Because
    // `await using` is sugar for try/finally and C# replaces (not suppresses) the original
    // exception when the finally throws, a clean DisposeAsync surfaces the original
    // TimeoutException — and a throwing DisposeAsync would surface a different type. So the
    // single Assert.IsType<TimeoutException> check pins both halves of the contract:
    //   1. BeginAsync propagates the AcquireAsync failure
    //   2. DisposeAsync ran cleanly (no exception type substitution)

    private readonly SqlConnection _connection = new(Configuration.DefaultConnectingString);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_mssql_provisioning_uow_begin_throws_it_should_dispose_without_throwing()
    {
        // Arrange — fake lock that fails Acquire after the transaction has been opened
        var failingLock = new FakeMsSqlAdvisoryLock(
            throwOnAcquire: new TimeoutException("forced lock-acquisition failure for spec 0028 §B.3 test"));

        // Act — capture whatever exception ultimately surfaces from the `await using` scope
        var thrown = await Record.ExceptionAsync(async () =>
        {
            await using var uow = new MsSqlProvisioningUnitOfWork(_connection, failingLock, NullLogger.Instance);
            await uow.BeginAsync(
                lockResource: "test_lock_resource",
                lockTimeout: TimeSpan.FromSeconds(5),
                cancellationToken: CancellationToken.None);
        });

        // Assert — the surfaced exception is the BeginAsync TimeoutException; DisposeAsync did
        // not throw (otherwise its exception would have replaced the TimeoutException).
        Assert.IsType<TimeoutException>(thrown);
    }
}
