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

public class When_mysql_provisioning_uow_rollback_observes_release_lock_tri_state_it_should_log_without_throwing : IAsyncLifetime
{
    // Per spec 0027 Item M / ADR 0057 §5b / ADR 0058 §B.3: MySQL's RELEASE_LOCK has three
    // outcomes — 1 (released by this session), 0 (lock held by another session — diagnostic
    // anomaly because we just acquired it), NULL (lock did not exist — same anomaly). The UoW
    // MUST preserve this distinction in a Warning-level log entry on Rollback (and Commit —
    // same diagnostic per `tasks.md` line 394; that arm is exercised at runtime in 5.3.b's
    // happy path but the Warning-emitting impl change for ALL non-true outcomes lands here).
    //
    // Equally important: RollbackAsync MUST NOT throw — disposal-style semantics (see
    // IAmAProvisioningUnitOfWork.RollbackAsync XML-doc, lines 113-114). The runner reaches
    // RollbackAsync from its catch path and must not have its unwind hijacked by a release
    // diagnostic.
    //
    // The UoW only knows the lockResource (== lockKey — the runner derives it via
    // MySqlMigrationLockName.For and passes it through BeginAsync), so the UoW-level Warning
    // names lockResource rather than tableName; the runner can still emit a richer log entry
    // with tableName context if it wants when Phase 7 rewires the runner onto the UoW. NF1 is
    // satisfied at the system level: the marker convention ("NULL" vs "0") and the meaning
    // text ("lock did not exist" vs "lock held by another session") are preserved verbatim
    // from the existing runner emission at MySqlBoxMigrationRunner.cs:138-141.
    //
    // The current 5.3.a/5.3.b stub — `Task RollbackAsync(...) => Task.CompletedTask` —
    // never calls ReleaseAsync, so all three arms fail RED on the ReleasedKey assertion
    // before any Warning-content assertion can run.

    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Should_complete_with_no_warning_when_release_returns_true() =>
        await AssertRollbackBehavior(releaseResult: true, expectWarning: false, expectedMarker: null);

    [Fact]
    public async Task Should_log_warning_with_marker_zero_when_release_returns_false() =>
        await AssertRollbackBehavior(releaseResult: false, expectWarning: true, expectedMarker: "0");

    [Fact]
    public async Task Should_log_warning_with_marker_NULL_when_release_returns_null() =>
        await AssertRollbackBehavior(releaseResult: null, expectWarning: true, expectedMarker: "NULL");

    private async Task AssertRollbackBehavior(bool? releaseResult, bool expectWarning, string? expectedMarker)
    {
        // Arrange — fake lock returns the parameterised tri-state outcome from RELEASE_LOCK;
        // capturing logger collects every entry so we can assert on level + message.
        var advisoryLock = new FakeMySqlAdvisoryLock(releaseResult);
        var logger = new CapturingLogger();

        await using var uow = new MySqlProvisioningUnitOfWork(_connection, advisoryLock, logger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Act — RollbackAsync must complete normally for ALL three outcomes per the
        // disposal-style "MUST NOT throw" contract on IAmAProvisioningUnitOfWork.RollbackAsync.
        var thrown = await Record.ExceptionAsync(() => uow.RollbackAsync(CancellationToken.None));

        // Assert — RollbackAsync did not throw, regardless of release outcome.
        Assert.Null(thrown);

        // Assert — RollbackAsync ALWAYS calls ReleaseAsync (lock release is the unwind's
        // primary purpose; the tri-state diagnostic is a side-channel observation on the
        // result, not a gate on whether to release).
        Assert.Equal("test_lock_resource", advisoryLock.ReleasedKey);

        // Assert — Warning emission matches the tri-state contract.
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        if (expectWarning)
        {
            // Exactly one Warning per non-true outcome, naming the lock resource and the
            // marker that disambiguates "0" (lock held by another session) from "NULL"
            // (lock did not exist) — preserving the spec 0027 Item M distinction.
            Assert.Single(warnings);
            Assert.Contains("test_lock_resource", warnings[0].Message, StringComparison.Ordinal);
            Assert.Contains(expectedMarker!, warnings[0].Message, StringComparison.Ordinal);
            Assert.Null(warnings[0].Exception);
        }
        else
        {
            // Happy-path RELEASE_LOCK = 1: no Warning entry — silence is the success signal.
            Assert.Empty(warnings);
        }
    }
}
