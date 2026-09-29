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

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// A <c>RELEASE_LOCK</c> that returns 0 or NULL after a migration is logged as one Warning naming the
/// table, the lock key and the result, and the migration still completes: the chain has already
/// committed (ADR 0057 §5b).
/// </summary>
public class MySqlAdvisoryReleaseLockNonTrueTests
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_release_returns_false_it_should_log_one_warning_and_complete_migration()
    {
        await AssertSingleWarningAndMigrationCompletes(
            releaseResult: false,
            expectedResultMarker: "0");
    }

    [Test]
    public async Task When_release_returns_null_it_should_log_one_warning_and_complete_migration()
    {
        await AssertSingleWarningAndMigrationCompletes(
            releaseResult: null,
            expectedResultMarker: "NULL");
    }

    [Test]
    public async Task When_release_returns_true_it_should_complete_migration_with_no_warning()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var fakeLock = new FakeMySqlAdvisoryLock(releaseResult: true);
        var capturingLogger = new CapturingLogger();

        var runner = new MySqlBoxMigrationRunner(
            new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30), fakeLock, capturingLogger);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint);

        //Assert
        await Assert.That(await GetBoxTableCount()).IsEqualTo(1);
        await Assert.That(System.Linq.Enumerable.Any(capturingLogger.Entries, e => e.Level == LogLevel.Warning)).IsFalse();
    }

    private async Task AssertSingleWarningAndMigrationCompletes(bool? releaseResult, string expectedResultMarker)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var fakeLock = new FakeMySqlAdvisoryLock(releaseResult);
        var capturingLogger = new CapturingLogger();

        var runner = new MySqlBoxMigrationRunner(
            new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30), fakeLock, capturingLogger);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint);

        //Assert
        await Assert.That(await GetBoxTableCount()).IsEqualTo(1);

        var expectedLockKey = MySqlMigrationLockName.For("BrighterTests", _tableName);
        await Assert.That(fakeLock.AcquiredKey).IsEqualTo(expectedLockKey);
        await Assert.That(fakeLock.ReleasedKey).IsEqualTo(expectedLockKey);

        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        await Assert.That(warnings).HasSingleItem();
        await Assert.That(warnings[0].Message).Contains(_tableName);
        await Assert.That(warnings[0].Message).Contains(expectedLockKey);
        await Assert.That(warnings[0].Message).Contains(expectedResultMarker);
        await Assert.That(warnings[0].Exception).IsNull();
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

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

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
