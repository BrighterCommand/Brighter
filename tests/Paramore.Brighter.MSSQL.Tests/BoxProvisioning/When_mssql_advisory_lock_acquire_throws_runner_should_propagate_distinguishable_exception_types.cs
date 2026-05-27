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
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_advisory_lock_acquire_throws_runner_should_propagate_distinguishable_exception_types : IAsyncLifetime
{
    // sp_getapplock returns one of four negative codes when acquisition fails: -1 (timeout),
    // -2 (cancelled), -3 (deadlock victim), -999 (parameter validation / call error). The
    // pre-Item-N runner collapses every negative result into a generic TimeoutException, so an
    // operator catching the exception cannot tell a transient timeout apart from a deadlock or
    // a misconfigured lock resource. Item N introduces an IMsSqlAdvisoryLock abstraction whose
    // default implementation throws a distinguishable exception type per code:
    //   -1   → TimeoutException
    //   -2   → OperationCanceledException
    //   -3   → MigrationLockDeadlockException (new)
    //   -999 → ArgumentException
    // The runner's contract per ADR 0057 §5b is to propagate each exception unchanged so the
    // operator catches the right type. These Facts pin that contract — each injects a fake
    // IMsSqlAdvisoryLock whose AcquireAsync throws a specific exception type, and asserts the
    // runner surfaces it without wrapping. The happy-path Fact asserts the migration completes
    // when AcquireAsync succeeds so a passing fake doesn't mask a runner regression.

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_propagate_TimeoutException_when_acquire_throws_TimeoutException()
    {
        await AssertRunnerPropagatesAcquireException(
            new TimeoutException("forced -1 timeout for spec 0027 Item N test"));
    }

    [Fact]
    public async Task Should_propagate_OperationCanceledException_when_acquire_throws_OperationCanceledException()
    {
        await AssertRunnerPropagatesAcquireException(
            new OperationCanceledException("forced -2 cancellation for spec 0027 Item N test"));
    }

    [Fact]
    public async Task Should_propagate_MigrationLockDeadlockException_when_acquire_throws_MigrationLockDeadlockException()
    {
        // -3 is the new path: MigrationLockDeadlockException is introduced by Item N so an
        // operator can distinguish a deadlock victim from a generic lock timeout (-1) and
        // respond appropriately (e.g. exponential backoff before retry rather than failing
        // the deployment).
        await AssertRunnerPropagatesAcquireException(
            new MigrationLockDeadlockException("forced -3 deadlock for spec 0027 Item N test"));
    }

    [Fact]
    public async Task Should_propagate_ArgumentException_when_acquire_throws_ArgumentException()
    {
        await AssertRunnerPropagatesAcquireException(
            new ArgumentException("forced -999 parameter validation for spec 0027 Item N test"));
    }

    [Fact]
    public async Task Should_complete_migration_when_acquire_succeeds()
    {
        //Arrange — happy-path fake: AcquireAsync is a no-op success. The runner's real DDL
        //          executes against the real MSSQL container under a real transaction.
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

        //Assert — migration committed normally and the fake observed the acquire under the
        //         expected lock resource (BrighterMigration_<schema>.<table> per ADR §5b —
        //         the schema qualifier prevents same-named tables in different schemas
        //         sharing a single advisory lock).
        Assert.Equal(1, await GetBoxTableCount());
        Assert.Equal($"BrighterMigration_dbo.{_tableName}", fakeLock.AcquiredResource);
    }

    private async Task AssertRunnerPropagatesAcquireException(Exception toThrow)
    {
        //Arrange — fake acquire throws the exception under test. We do NOT need MsSqlTestHelper
        //          to set up the database here because the runner must throw before any DDL
        //          fires; the parameterised assertion below proves no half-state was left.
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new MsSqlOutboxMigrationCatalog();

        var fakeLock = new FakeMsSqlAdvisoryLock(throwOnAcquire: toThrow);

        var runner = new MsSqlBoxMigrationRunner(
            catalog, config, TimeSpan.FromSeconds(30), fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert — runner surfaces the same exception type without wrapping.
        var thrown = await Assert.ThrowsAsync(toThrow.GetType(), () =>
            runner.MigrateAsync(
                _tableName, schemaName: null, BoxType.Outbox, freshHint));
        Assert.Same(toThrow, thrown);

        //Assert — the fake observed the acquire attempt under the expected lock resource
        //         (the resource name itself is part of the abstraction's contract — it must
        //         come from the runner, not the fake).
        Assert.Equal($"BrighterMigration_dbo.{_tableName}", fakeLock.AcquiredResource);

        //Assert — no DDL fired: the box table was NOT created. Acquisition is the first action
        //         inside the lock-bearing transaction, so a throw rolls back cleanly.
        Assert.Equal(0, await GetBoxTableCount());
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

    public Task InitializeAsync() => Task.CompletedTask;

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
