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

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlProvisioningUnitOfWorkCommitTests
{
    // Per ADR 0058 §B.1 / ADR 0057 §5a / §5b: MySQL is the transactionless backend in the
    // relational family; CommitAsync's only meaningful side-effect is to release the
    // session-level GET_LOCK acquired by BeginAsync. There is no transaction to commit
    // (Transaction is always null per 5.3.a), and the runner relies on this so that the
    // outer composite operation can release contention as soon as the migration's last DDL
    // statement has implicitly committed server-side.
    //
    // The test wires a FakeMySqlAdvisoryLock with releaseResult: true (the happy-path
    // RELEASE_LOCK = 1 outcome — lock existed and was released by this session). Two-fact
    // pin:
    //   1. _advisoryLock.ReleasedKey == "test_lock_resource" — RELEASE_LOCK was issued
    //      with the same key BeginAsync stored. The current 5.3.a stub
    //      (`Task CommitAsync(...) => Task.CompletedTask`) does not call ReleaseAsync, so
    //      this assertion is the RED discriminator.
    //   2. uow.Transaction is null — Transaction stays null both before and after
    //      CommitAsync. Pins "no transaction commit happened" structurally: any impl that
    //      tried to commit would need to first open a tx (and 5.3.a pinned that BeginAsync
    //      does NOT), so a non-null Transaction would be the only signal that commit logic
    //      had been wired in error.
    //
    // The tri-state RELEASE_LOCK diagnostic logging contract (NULL → out-of-memory/KILL,
    // 0 → not-held-by-this-session) is the §5b discriminator pinned by 5.3.c — happy path
    // here uses the bool? = true outcome so no Warning is expected, but this test does NOT
    // assert "no warning" since that is more naturally the responsibility of 5.3.c's
    // tri-state pin.
    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);
    private readonly FakeMySqlAdvisoryLock _advisoryLock = new(releaseResult: true);

    [Before(Test)]
    public async Task InitializeAsync() => await _connection.OpenAsync();

    [After(Test)]
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Test]
    public async Task When_mysql_provisioning_uow_commit_async_is_called_it_should_release_get_lock_without_committing_transaction()
    {
        // Arrange — UoW under test, BeginAsync acquires lock per 5.3.a contract
        await using var uow = new MySqlProvisioningUnitOfWork(
            _connection, _advisoryLock, NullLogger.Instance);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Act
        await uow.CommitAsync(CancellationToken.None);

        // Assert: RELEASE_LOCK was issued with the same key BeginAsync used.
        await Assert.That(_advisoryLock.ReleasedKey).IsEqualTo("test_lock_resource");
        // Assert: no transaction was opened or committed — Transaction stays null. Per ADR
        // 0057 §5a the MySQL UoW never opens a transaction; CommitAsync's job is lock
        // release only.
        await Assert.That(uow.Transaction).IsNull();
    }
}