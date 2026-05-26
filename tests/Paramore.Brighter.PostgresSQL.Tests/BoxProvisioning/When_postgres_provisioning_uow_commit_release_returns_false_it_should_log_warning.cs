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

public class When_postgres_provisioning_uow_commit_release_returns_false_it_should_log_warning : IAsyncLifetime
{
    // Per ADR 0057 §5b: pg_advisory_unlock returns false when the calling session does not
    // currently hold the named lock — a diagnostic anomaly because the UoW just acquired it.
    // The release call happens after CommitAsync's tx-commit succeeded, so we cannot fail the
    // unit-of-work; we surface the condition through a Warning-level log entry naming the lock
    // resource and continue.
    //
    // This obligation was originally emitted by the runner under spec 0027. After the Phase 5.2
    // unit-of-work refactor the contract moves to CommitAsync — the runner no longer owns the
    // release call. The runner-level integration test
    // When_postgres_advisory_unlock_returns_false_runner_should_log_warning_and_complete_normally
    // exercises the same behaviour through the public MigrateAsync surface; this test pins the
    // contract directly at the UoW boundary so a future regression surfaces here without
    // requiring a real database round-trip through the runner.

    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(releaseResult: false);
    private readonly CapturingLogger _logger = new();

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Should_log_one_warning_when_release_returns_false_and_complete_normally()
    {
        // Arrange
        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, _advisoryLock, _logger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Act — CommitAsync must NOT throw despite the false release result; the underlying
        // tx is already committed by the time the release call is made, so failing here would
        // leave the caller with an inconsistent view of the unit-of-work.
        var thrown = await Record.ExceptionAsync(() => uow.CommitAsync(CancellationToken.None));

        // Assert: completion was clean.
        Assert.Null(thrown);
        // Assert: pg_advisory_unlock was called with the same lock resource passed to BeginAsync.
        Assert.Equal("test_lock_resource", _advisoryLock.ReleasedKey);
        // Assert: exactly one Warning entry whose message names the lock resource. The runner
        // integration test additionally requires the tableName (e.g. "test_outbox_xxx") to be
        // a substring of the message — but the lock resource format is
        // "BrighterMigration_<schema>.<tableName>", so a message that names the lock resource
        // names the table by extension. No exception attached (diagnostic only).
        var warnings = _logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("test_lock_resource", warnings[0].Message, StringComparison.Ordinal);
        Assert.Null(warnings[0].Exception);
    }
}
