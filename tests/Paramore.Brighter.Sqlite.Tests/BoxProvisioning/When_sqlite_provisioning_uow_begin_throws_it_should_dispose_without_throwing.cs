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
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class ProvisioningUnitOfWorkBeginThrowsTests
{
    // Per ADR 0058 §B.3: the runner declares the UoW with `await using`, so DisposeAsync runs
    // on every exit path — including when BeginAsync itself throws. In that case the UoW is
    // in a partial-init state (no transaction was opened — BEGIN IMMEDIATE failed) and
    // DisposeAsync MUST tolerate that state without throwing — otherwise the runner's catch
    // path sees the disposal exception instead of the underlying connection failure and the
    // operator loses the actionable diagnostic.
    //
    // SQLite's failure mode is shape-different from MSSQL/Postgres/MySQL: the SQLite UoW has
    // no IAmA*AdvisoryLock collaborator (per ADR 0057 §4 — BEGIN IMMEDIATE IS the lock), so
    // there is no fake-lock to wire up to throw. The simplest equivalent is a closed
    // connection — Microsoft.Data.Sqlite throws InvalidOperationException
    // ("ConnectionString property has not been initialized." / "BeginTransaction can only be
    // called when the connection is open." depending on driver version) before any state is
    // captured by the UoW.
    //
    // The whole `await using` block is wrapped in Record.ExceptionAsync. Because `await using`
    // is sugar for try/finally and C# replaces (not suppresses) the original exception when
    // the finally throws, a clean DisposeAsync surfaces the original InvalidOperationException
    // — and a throwing DisposeAsync would surface a different type. The single
    // Assert.IsType<InvalidOperationException> check therefore pins both halves of the contract:
    //   1. BeginAsync propagates the BEGIN IMMEDIATE failure.
    //   2. DisposeAsync ran cleanly (no exception type substitution).
    //
    // GREEN-from-the-start regression guard: 5.4.a's DisposeAsync (`if (_transaction is null)
    // return default;`) already satisfies the contract because BeginTransaction throws before
    // _transaction is assigned. This test exists to prevent a future "be helpful and
    // unconditionally clean up" mutation from regressing the partial-init contract.

    [Fact]
    public async Task When_sqlite_provisioning_uow_begin_throws_it_should_dispose_without_throwing()
    {
        // Arrange — closed connection. SqliteConnection.BeginTransaction throws
        // InvalidOperationException when the connection is not in the Open state.
        await using var closedConnection = new SqliteConnection("Data Source=:memory:");

        // Act — capture whatever exception ultimately surfaces from the `await using` scope.
        var thrown = await Record.ExceptionAsync(async () =>
        {
            await using var uow = new SqliteProvisioningUnitOfWork(closedConnection, NullLogger.Instance);
            await uow.BeginAsync(
                lockResource: "test_lock_resource",
                lockTimeout: TimeSpan.FromSeconds(5),
                cancellationToken: CancellationToken.None);
        });

        // Assert — the surfaced exception is the BeginAsync InvalidOperationException;
        // DisposeAsync did not throw (otherwise its exception would have replaced the IOE).
        Assert.IsType<InvalidOperationException>(thrown);
    }
}
