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

public class When_mysql_provisioning_uow_release_returns_non_true_with_table_name_it_should_log_warning_containing_table_name : IAsyncLifetime
{
    // Phase 5.3 regression guard exposed by Phase 7.3a (orchestration shift from legacy
    // runner-owned RELEASE_LOCK Warning to the inherited base-template orchestration). The
    // legacy runner's Warning named both TableName and LockKey as separate template parameters;
    // the UoW only knows lockResource — which equals the GET_LOCK key MySqlMigrationLockName.For
    // produced, and that key hash-truncates the raw table name for long composites (≥ 64 chars
    // including prefix). Asserting tableName as a substring of the UoW Warning fails in that
    // case unless the UoW knows the un-hashed table name separately.
    //
    // Per spec 0027 Item M / ADR 0057 §5b / ADR 0058 §B.3: a non-true RELEASE_LOCK is a
    // diagnostic anomaly the UoW MUST surface as a Warning with no-information-loss (NF1)
    // relative to the legacy runner's emission. The legacy slots: TableName, LockKey, Result,
    // Marker, Meaning. Restoring TableName requires the UoW to receive it; this test pins the
    // contract that the optional tableName ctor parameter, when supplied, is named in the
    // Warning alongside lockResource.

    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);

    public async Task InitializeAsync() => await _connection.OpenAsync();
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Theory]
    [InlineData(false, "0")]
    [InlineData(null, "NULL")]
    public async Task Should_log_warning_naming_table_name_and_lock_resource_when_release_is_non_true(
        bool? releaseResult, string expectedMarker)
    {
        // Arrange — UoW receives a tableName (the runner provides this from the MigrateAsync
        // entry-point), lockResource = the hash-truncated 64-char-safe GET_LOCK key, and a fake
        // advisory lock whose Release returns the parameterised non-true outcome.
        const string tableName = "test_outbox_3f9d2a1b8c7e4f0a9d6b5c3a1f8e7d2b";
        const string lockResource = "BrighterMigration_BrighterTests.test_outbox_3f9_a1b2c3d4e5f60718";

        var advisoryLock = new FakeMySqlAdvisoryLock(releaseResult);
        var logger = new CapturingLogger();

        await using var uow = new MySqlProvisioningUnitOfWork(_connection, advisoryLock, logger, tableName);
        await uow.BeginAsync(lockResource, TimeSpan.FromSeconds(5), CancellationToken.None);

        // Act — Commit/Rollback both go through the same ReleaseLockAndLogTriStateAsync helper
        // per Phase 5.3.c; exercising Commit here pins the Warning emission contract for the
        // happy-path completion of MigrateAsync (the runner-level integration test exercises
        // Commit too — the runner's catch path only fires when the chain throws).
        var thrown = await Record.ExceptionAsync(() => uow.CommitAsync(CancellationToken.None));

        // Assert — Commit completed without throwing, RELEASE_LOCK was invoked with the
        // lockResource verbatim (UoW does not transform the key).
        Assert.Null(thrown);
        Assert.Equal(lockResource, advisoryLock.ReleasedKey);

        // Assert — single Warning entry containing BOTH the raw tableName and the lockResource
        // (the hashed GET_LOCK key). Restores the legacy runner's TableName+LockKey emission
        // shape per NF1.
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains(tableName, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(lockResource, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(expectedMarker, warnings[0].Message, StringComparison.Ordinal);
        Assert.Null(warnings[0].Exception);
    }
}
