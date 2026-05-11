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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_advisory_lock_acquire_throws_during_begin_async_runner_should_not_call_commit_or_rollback : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_propagate_MigrationLockDeadlockException_without_invoking_commit_or_rollback_and_dispose_runs()
    {
        //Arrange — fake IMsSqlAdvisoryLock whose AcquireAsync throws MigrationLockDeadlockException
        // (spec 0027 Item N -3 deadlock-victim path). The runner's CreateUnitOfWorkAsync hook is
        // overridden to wrap the real MsSqlProvisioningUnitOfWork with a spy that records whether
        // CommitAsync, RollbackAsync, and DisposeAsync were invoked. Per RelationalBoxMigrationRunnerBase
        // (ADR 0058 §B.3): the try-block is entered AFTER `await uow.BeginAsync(...)` returns, so
        // a throw from BeginAsync must propagate WITHOUT entering the try (CommitAsync) or its
        // catch (explicit RollbackAsync). The `await using uow = ...` declaration sits OUTSIDE
        // that try, so DisposeAsync MUST still run on the way out.
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);

        var deadlock = new MigrationLockDeadlockException(
            "forced -3 deadlock victim for spec 0028 Phase 10.2 BeginAsync-throws contract test");
        var fakeLock = new FakeMsSqlAdvisoryLock(throwOnAcquire: deadlock);

        var spyingRunner = new SpyingMsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30), fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — runner's BeginAsync throws because AcquireAsync throws; per ADR §B.3 the throw
        // propagates from MigrateAsync without the runner's try-block ever executing.
        var thrown = await Assert.ThrowsAsync<MigrationLockDeadlockException>(() => spyingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, freshHint));

        //Assert — the original deadlock exception propagates unwrapped (per spec 0027 Item N
        // distinguishable-exception contract — the operator must see the typed exception so
        // they can choose retry-with-backoff vs deployment failure).
        Assert.Same(deadlock, thrown);

        //Assert — the runner DID construct the UoW (BeginAsync is called on it; the throw comes
        // from inside BeginAsync itself, not before it), so the spy is observable.
        var spy = spyingRunner.LastUnitOfWork;
        Assert.NotNull(spy);

        //Assert — CommitAsync was NEVER called: the runner only commits inside its try-block,
        // and the throw from BeginAsync prevents the try-block from being entered.
        Assert.False(spy!.CommitInvoked,
            "CommitAsync must NOT be called when BeginAsync throws — the runner's try-block never executed.");

        //Assert — RollbackAsync was NEVER called: the runner's catch-path RollbackAsync is the
        // unwind for failures INSIDE the try-block (after BeginAsync succeeds). A throw from
        // BeginAsync itself happens before the try is entered, so the catch is never hit. The
        // partially-initialised transaction (BeginTransaction succeeded, AcquireAsync threw) is
        // released via DisposeAsync on the implicit rollback that SqlTransaction.Dispose performs.
        Assert.False(spy.RollbackInvoked,
            "RollbackAsync must NOT be called when BeginAsync throws — the catch-block guards the try-block, not BeginAsync.");

        //Assert — DisposeAsync IS called via the `await using uow = ...` declaration sitting
        // OUTSIDE the runner's try-block. This is the contract that releases the partial-init
        // transaction back to SQL Server (sp_getapplock with @LockOwner='Transaction' is bound
        // to the SqlTransaction's lifetime; SqlTransaction.Dispose implicitly rolls back, which
        // in turn releases any application lock state — though in this scenario AcquireAsync
        // threw before any lock was registered).
        Assert.True(spy.DisposeInvoked,
            "DisposeAsync MUST run via `await using` even when BeginAsync throws — otherwise the partial-init transaction leaks.");

        //Assert — a fresh runner (no fake lock) immediately succeeds against the same lock
        // resource, proving the failed acquire left no lingering server-side state. If the
        // partial transaction had been left undisposed, the next BeginTransaction on the same
        // connection-pool slot would block or error.
        var freshRunner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(5));
        await freshRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, freshHint, CancellationToken.None);

        Assert.True(TableExists(),
            "A fresh runner with no failing lock must complete the migration against the same resource after the failed acquire was cleaned up.");
    }

    private bool TableExists()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sys.tables WHERE name = @TableName AND schema_id = SCHEMA_ID('dbo')";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return (int)command.ExecuteScalar()! > 0;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            dropTable.ExecuteNonQuery();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NOT NULL
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort cleanup
        }
        await Task.CompletedTask;
    }
}

file sealed class SpyingMsSqlBoxMigrationRunner : MsSqlBoxMigrationRunner
{
    public LifecycleSpyingUnitOfWork? LastUnitOfWork { get; private set; }

    public SpyingMsSqlBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IMsSqlAdvisoryLock advisoryLock)
        : base(configuration, lockTimeout, advisoryLock)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, cancellationToken);
        LastUnitOfWork = new LifecycleSpyingUnitOfWork(inner);
        return LastUnitOfWork;
    }
}

file sealed class LifecycleSpyingUnitOfWork : IAmAProvisioningUnitOfWork<SqlTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<SqlTransaction> _inner;

    public bool CommitInvoked { get; private set; }
    public bool RollbackInvoked { get; private set; }
    public bool DisposeInvoked { get; private set; }

    public LifecycleSpyingUnitOfWork(IAmAProvisioningUnitOfWork<SqlTransaction> inner) => _inner = inner;

    public SqlTransaction? Transaction => _inner.Transaction;

    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
        => _inner.BeginAsync(lockResource, lockTimeout, cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitInvoked = true;
        return _inner.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackInvoked = true;
        return _inner.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        DisposeInvoked = true;
        return _inner.DisposeAsync();
    }
}
