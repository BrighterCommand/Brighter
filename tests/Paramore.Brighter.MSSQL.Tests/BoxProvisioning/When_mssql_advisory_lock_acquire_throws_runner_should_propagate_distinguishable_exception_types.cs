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
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// The runner propagates, unwrapped and before any DDL, the exception <see cref="IMsSqlAdvisoryLock"/>
/// throws for each <c>sp_getapplock</c> failure — timeout (-1), cancellation (-2), deadlock victim (-3),
/// call error (-999) — so an operator can tell them apart (ADR 0057 §5b). When the acquire succeeds,
/// the migration completes.
/// </summary>
public class MsSqlAdvisoryLockAcquireExceptionPropagationTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_acquire_throws_timeout_exception_it_should_propagate_timeout_exception()
    {
        await AssertRunnerPropagatesAcquireException(
            new TimeoutException("forced -1 timeout for spec 0027 Item N test"));
    }

    [Test]
    public async Task When_acquire_throws_operation_canceled_exception_it_should_propagate_operation_canceled_exception()
    {
        await AssertRunnerPropagatesAcquireException(
            new OperationCanceledException("forced -2 cancellation for spec 0027 Item N test"));
    }

    [Test]
    public async Task When_acquire_throws_migration_lock_deadlock_exception_it_should_propagate_migration_lock_deadlock_exception()
    {
        await AssertRunnerPropagatesAcquireException(
            new MigrationLockDeadlockException("forced -3 deadlock for spec 0027 Item N test"));
    }

    [Test]
    public async Task When_acquire_throws_argument_exception_it_should_propagate_argument_exception()
    {
        await AssertRunnerPropagatesAcquireException(
            new ArgumentException("forced -999 parameter validation for spec 0027 Item N test"));
    }

    [Test]
    public async Task When_acquire_succeeds_it_should_complete_migration()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new MsSqlOutboxMigrationCatalog();

        var fakeLock = new FakeMsSqlAdvisoryLock(throwOnAcquire: null);

        var runner = new MsSqlBoxMigrationRunner(
            catalog, config, TimeSpan.FromSeconds(30), fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint);

        //Assert
        await Assert.That(await GetBoxTableCount()).IsEqualTo(1);
        await Assert.That(fakeLock.AcquiredResource).IsEqualTo($"BrighterMigration_dbo.{_tableName}");
    }

    private async Task AssertRunnerPropagatesAcquireException(Exception toThrow)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new MsSqlOutboxMigrationCatalog();

        var fakeLock = new FakeMsSqlAdvisoryLock(throwOnAcquire: toThrow);

        var runner = new MsSqlBoxMigrationRunner(
            catalog, config, TimeSpan.FromSeconds(30), fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert
        var thrown = await TestExceptionRecorder.CaptureAsync(() => runner.MigrateAsync(_tableName, schemaName: null, BoxType.Outbox, freshHint));
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.GetType()).IsEqualTo(toThrow.GetType());
        await Assert.That(thrown).IsSameReferenceAs(toThrow);

        await Assert.That(fakeLock.AcquiredResource).IsEqualTo($"BrighterMigration_dbo.{_tableName}");

        await Assert.That(await GetBoxTableCount()).IsEqualTo(0);
    }

    private async Task<int> GetBoxTableCount()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM sys.tables
WHERE name = @TableName AND schema_id = SCHEMA_ID('dbo')";
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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"IF OBJECT_ID(N'[dbo].[{_tableName}]', N'U') IS NOT NULL DROP TABLE [dbo].[{_tableName}]; " +
                $"IF OBJECT_ID(N'[dbo].[__BrighterMigrationHistory]', N'U') IS NOT NULL " +
                $"DELETE FROM [dbo].[__BrighterMigrationHistory] WHERE [BoxTableName] = '{_tableName}';";
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

}
