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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class When_mysql_advisory_release_lock_returns_non_true_runner_should_log_warning_and_complete_normally : IAsyncLifetime
{
    // MySQL RELEASE_LOCK has three outcomes: 1 (released by this session — true), 0 (lock
    // exists but held by another session — false), NULL (lock did not exist). The runner
    // just acquired the lock immediately before, so any non-true result is a diagnostic
    // anomaly. We surface it through a Warning-level log entry naming the table name, the
    // lock key, and the result code, then continue normally — the chain has already
    // committed by the time release runs. Per ADR 0057 §5b, this contract is the public
    // boundary between the runner and its IMySqlAdvisoryLock collaborator.

    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_log_one_warning_and_complete_migration_when_release_returns_false()
    {
        await AssertSingleWarningAndMigrationCompletes(
            releaseResult: false,
            expectedResultMarker: "0");
    }

    [Fact]
    public async Task Should_log_one_warning_and_complete_migration_when_release_returns_null()
    {
        await AssertSingleWarningAndMigrationCompletes(
            releaseResult: null,
            expectedResultMarker: "NULL");
    }

    [Fact]
    public async Task Should_complete_migration_with_no_warning_when_release_returns_true()
    {
        //Arrange — happy-path fake returns 1 (released by us).
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var migrations = MySqlOutboxMigrations.All(config);

        var fakeLock = new FakeMySqlAdvisoryLock(releaseResult: true);
        var capturingLogger = new CapturingLogger();

        var runner = new MySqlBoxMigrationRunner(
            config, TimeSpan.FromSeconds(30), fakeLock, capturingLogger);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, freshHint);

        //Assert — migration committed normally and no warning was emitted.
        Assert.Equal(1, await GetBoxTableCount());
        Assert.DoesNotContain(capturingLogger.Entries, e => e.Level == LogLevel.Warning);
    }

    private async Task AssertSingleWarningAndMigrationCompletes(bool? releaseResult, string expectedResultMarker)
    {
        //Arrange — runner uses a real MySQL connection for DDL, a fake advisory lock that
        //          returns the parameterised non-true result from Release, and a capturing
        //          logger so we can assert the warning emission.
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var migrations = MySqlOutboxMigrations.All(config);

        var fakeLock = new FakeMySqlAdvisoryLock(releaseResult);
        var capturingLogger = new CapturingLogger();

        var runner = new MySqlBoxMigrationRunner(
            config, TimeSpan.FromSeconds(30), fakeLock, capturingLogger);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — runner must not throw despite the non-true release result.
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, freshHint);

        //Assert — migration committed normally: box table created.
        Assert.Equal(1, await GetBoxTableCount());

        //Assert — fake observed acquire and release under the expected lock key
        //         (derived via the existing public MySqlMigrationLockName.For helper).
        //schemaName=null in MigrateAsync resolves to effectiveSchema=DATABASE() (the connection's
        //bound database — "BrighterTests" here) in the runner, and the lock key folds the schema
        //in to keep same-named tables in different schemas from sharing a lock. See Item O.
        var expectedLockKey = MySqlMigrationLockName.For("BrighterTests", _tableName);
        Assert.Equal(expectedLockKey, fakeLock.AcquiredKey);
        Assert.Equal(expectedLockKey, fakeLock.ReleasedKey);

        //Assert — exactly one Warning entry whose message names the table name, lock key,
        //         and result-code marker, with no exception attached (diagnostic only).
        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains(_tableName, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(expectedLockKey, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(expectedResultMarker, warnings[0].Message, StringComparison.Ordinal);
        Assert.Null(warnings[0].Exception);
    }

    private async Task<int> GetBoxTableCount()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"DROP TABLE IF EXISTS `{_tableName}`; " +
                $"DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = '{_tableName}';";
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

}
