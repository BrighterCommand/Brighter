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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class When_sqlite_migration_is_cancelled_mid_flight_it_should_rollback_releasing_writer_slot : IAsyncLifetime
{
    // Per-test DB file so the writer slot we hold during Task.Delay does not contend with
    // sibling tests in this assembly running against the shared `test.db`. Same isolation
    // pattern used by SqliteRunnerSqliteBusyContentionTests.
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_cancel_{Guid.NewGuid():N}.db");
    private readonly string _connectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    public When_sqlite_migration_is_cancelled_mid_flight_it_should_rollback_releasing_writer_slot()
    {
        _connectionString = $"Data Source={_dbPath}";
    }

    [Fact]
    public async Task Should_invoke_rollback_with_cancellation_token_none_and_release_writer_slot_when_caller_cancels_mid_flight()
    {
        //Arrange — the cancelling runner's RunFreshPathAsync override blocks on Task.Delay so
        // the caller's cancellation hits AFTER BeginAsync has opened the BEGIN IMMEDIATE
        // transaction and taken SQLite's database-wide RESERVED writer slot, AND BEFORE the
        // base calls CommitAsync. The UoW is wrapped by a spy that captures the token passed
        // to RollbackAsync from the runner's catch path.
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        var cancellingRunner = new CancellingSqliteBoxMigrationRunner(config);
        var staleHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(250));

        //Act — caller's CancellationToken is signalled while the runner is mid-flight inside
        // the (substituted) fresh-path hook.
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancellingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, staleHint, cts.Token));

        //Assert — the original OperationCanceledException propagates to the caller (per ADR §B.3).
        Assert.NotNull(thrown);

        //Assert — RollbackAsync IS invoked after the mid-flight cancellation.
        var spy = cancellingRunner.LastUnitOfWork;
        Assert.NotNull(spy);
        Assert.True(spy!.RollbackInvoked,
            "RollbackAsync should have been invoked after the mid-flight cancellation per ADR §B.3.");

        //Assert — the CancellationToken passed to RollbackAsync is CancellationToken.None, NOT
        // the caller's signalled token. SqliteTransaction.RollbackAsync short-circuits on a
        // signalled token and would throw OCE without performing the rollback; passing
        // CancellationToken.None per ADR §B.3 lets the rollback (and the implicit RESERVED-lock
        // release that comes with it) complete.
        Assert.False(spy.RollbackToken.IsCancellationRequested,
            "RollbackAsync should receive CancellationToken.None to ensure the writer-slot release completes.");
        Assert.Equal(CancellationToken.None, spy.RollbackToken);

        //Assert — the SQLite database-wide writer slot IS released. A fresh runner whose
        // BeginAsync issues BEGIN IMMEDIATE on the same database completes the migration
        // normally; the 5s lock timeout would expire and surface as SQLITE_BUSY (wrapped as
        // MigrationLockDeadlockException) if the writer slot were still held.
        var freshRunner = new SqliteBoxMigrationRunner(config, TimeSpan.FromSeconds(5));
        await freshRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, staleHint, CancellationToken.None);

        Assert.True(await TableExistsAsync(),
            "Subsequent migration should have created the outbox table after the writer slot was released.");
    }

    private async Task<bool> TableExistsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@TableName";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch
        {
            // Best-effort cleanup
        }
        await Task.CompletedTask;
    }
}

file sealed class CancellingSqliteBoxMigrationRunner : SqliteBoxMigrationRunner
{
    public RollbackTokenCapturingUnitOfWork? LastUnitOfWork { get; private set; }

    public CancellingSqliteBoxMigrationRunner(IAmARelationalDatabaseConfiguration configuration)
        : base(configuration)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqliteTransaction>> CreateUnitOfWorkAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, cancellationToken);
        LastUnitOfWork = new RollbackTokenCapturingUnitOfWork(inner);
        return LastUnitOfWork;
    }

    // Substitute fresh-path "work" with a delay long enough to be interrupted mid-flight by
    // the test's CancellationTokenSource.CancelAfter. The throw from Task.Delay is a
    // TaskCanceledException (subtype of OperationCanceledException) and is caught by the base
    // runner's catch path — which is the contract under test.
    protected override Task RunFreshPathAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
}

file sealed class RollbackTokenCapturingUnitOfWork : IAmAProvisioningUnitOfWork<SqliteTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<SqliteTransaction> _inner;

    public bool RollbackInvoked { get; private set; }
    public CancellationToken RollbackToken { get; private set; }

    public RollbackTokenCapturingUnitOfWork(IAmAProvisioningUnitOfWork<SqliteTransaction> inner) => _inner = inner;

    public SqliteTransaction? Transaction => _inner.Transaction;

    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
        => _inner.BeginAsync(lockResource, lockTimeout, cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken) => _inner.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackInvoked = true;
        RollbackToken = cancellationToken;
        return _inner.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
