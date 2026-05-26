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
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgres_provisioning_uow_rollback_release_throws_it_should_log_without_throwing : IAsyncLifetime
{
    // Per ADR 0058 §B.3: RollbackAsync MUST NOT throw — disposal-style semantics. The runner's
    // catch path is `catch { await uow.RollbackAsync(CancellationToken.None); throw; }`, so any
    // exception that escapes RollbackAsync masks the original migration error.
    //
    // PostgreSqlProvisioningUnitOfWork.RollbackAsync already guards the _transaction.RollbackAsync
    // call with `try/catch (InvalidOperationException)` because Npgsql throws that when a tx is
    // finalised. But pg_advisory_unlock executes SQL on the same connection — if the connection
    // is dead or the driver throws (NpgsqlException, ObjectDisposedException, etc.) the
    // unguarded ReleaseAsync call propagates straight through RollbackAsync, breaking the
    // contract and masking the original migration failure in the runner's catch.
    //
    // This test pins that the release-side throw is swallowed and logged the same way the
    // transaction-side InvalidOperationException already is: best-effort, no rethrow, Warning
    // entry preserving the lock-resource context.

    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Should_swallow_release_exception_and_log_warning()
    {
        // Arrange — release throws (simulates a dead connection / driver fault during
        // pg_advisory_unlock). NpgsqlException ctors are internal in modern Npgsql, so an
        // InvalidOperationException ("Connection is broken") is a representative driver-side
        // throw — the production guard catches Exception, not a specific subtype.
        var releaseFault = new InvalidOperationException("Connection is broken");
        var advisoryLock = new FakePostgreSqlAdvisoryLock(
            releaseResult: true,
            throwOnRelease: releaseFault);
        var capturingLogger = new CapturingLogger();

        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, advisoryLock, capturingLogger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Act — RollbackAsync MUST NOT throw even though ReleaseAsync does. This is the
        // disposal-style contract the runner's catch path depends on.
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(CancellationToken.None));

        // Assert — RollbackAsync swallowed the release-side throw.
        Assert.Null(thrown);

        // Assert — ReleaseAsync was attempted (the lock-key was recorded before the throw),
        // so the unwind genuinely tried to release rather than skipping it.
        Assert.Equal("test_lock_resource", advisoryLock.ReleasedKey);

        // Assert — a Warning was logged naming the lock resource and carrying the original
        // exception so operators can correlate against the broken connection.
        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("test_lock_resource", warnings[0].Message, StringComparison.Ordinal);
        Assert.Same(releaseFault, warnings[0].Exception);
    }
}
