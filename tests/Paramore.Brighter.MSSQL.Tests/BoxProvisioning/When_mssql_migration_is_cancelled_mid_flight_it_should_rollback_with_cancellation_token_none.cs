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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlMigrationCancellationRollbackTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_mssql_migration_is_cancelled_mid_flight_it_should_rollback_with_cancellation_token_none()
    {
        //Arrange — fresh database; the cancelling runner's RunFreshPathAsync override blocks
        // on Task.Delay so the caller's cancellation hits AFTER BeginAsync has opened the
        // SqlTransaction and acquired the sp_getapplock advisory lock, AND BEFORE the base
        // calls CommitAsync. The UoW is wrapped by a spy that captures the token passed to
        // RollbackAsync from the runner's catch path.
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new MsSqlOutboxMigrationCatalog();

        var cancellingRunner = new CancellingMsSqlBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(30));
        var staleHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(250));

        //Act — caller's CancellationToken is signalled while the runner is mid-flight inside
        // the (substituted) fresh-path hook.
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancellingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint, cts.Token));

        //Assert — the original OperationCanceledException propagates to the caller (per ADR §B.3).
        Assert.NotNull(thrown);

        //Assert — RollbackAsync IS invoked after the mid-flight cancellation.
        var spy = cancellingRunner.LastUnitOfWork;
        Assert.NotNull(spy);
        Assert.True(spy!.RollbackInvoked,
            "RollbackAsync should have been invoked after the mid-flight cancellation per ADR §B.3.");

        //Assert — the CancellationToken passed to RollbackAsync is CancellationToken.None, NOT
        // the caller's signalled token. Per ADR §B.3, passing the signalled token here would
        // cause RollbackAsync itself to throw OCE and abandon the unwind, leaving the
        // sp_getapplock attached to a zombied transaction.
        Assert.False(spy.RollbackToken.IsCancellationRequested,
            "RollbackAsync should receive CancellationToken.None to ensure lock release completes.");
        Assert.Equal(CancellationToken.None, spy.RollbackToken);

        //Assert — the MSSQL transaction-scoped advisory lock IS released. Verified by a fresh
        // runner whose BeginAsync acquires the same per-table lock resource without waiting
        // (lock timeout is 5s) and completes the migration normally. If the rollback had been
        // short-circuited by a signalled CT the sp_getapplock would still be held by the
        // zombied transaction and the second BeginAsync would block until the 5s timeout
        // elapsed and throw MigrationLockDeadlockException.
        var freshRunner = new MsSqlBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(5));
        await freshRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint, CancellationToken.None);

        Assert.True(TableExists(),
            "Subsequent migration should have created the outbox table after the lock was released.");
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

file sealed class CancellingMsSqlBoxMigrationRunner : MsSqlBoxMigrationRunner
{
    public RollbackTokenCapturingUnitOfWork? LastUnitOfWork { get; private set; }

    public CancellingMsSqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout)
        : base(catalog, configuration, lockTimeout)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, schemaName, tableName, cancellationToken);
        LastUnitOfWork = new RollbackTokenCapturingUnitOfWork(inner);
        return LastUnitOfWork;
    }

    // Substitute fresh-path "work" with a delay long enough to be interrupted mid-flight by
    // the test's CancellationTokenSource.CancelAfter. The throw from Task.Delay is a
    // TaskCanceledException (subtype of OperationCanceledException) and is caught by the
    // base runner's catch path — which is the contract under test.
    protected override Task RunFreshPathAsync(
        SqlConnection connection, SqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
}

file sealed class RollbackTokenCapturingUnitOfWork : IAmAProvisioningUnitOfWork<SqlTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<SqlTransaction> _inner;

    public bool RollbackInvoked { get; private set; }
    public CancellationToken RollbackToken { get; private set; }

    public RollbackTokenCapturingUnitOfWork(IAmAProvisioningUnitOfWork<SqlTransaction> inner) => _inner = inner;

    public SqlTransaction? Transaction => _inner.Transaction;

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
