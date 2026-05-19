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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class When_mysql_migration_is_cancelled_mid_flight_it_should_release_get_lock : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_invoke_rollback_with_cancellation_token_none_and_release_get_lock_when_caller_cancels_mid_flight()
    {
        //Arrange — the cancelling runner's RunFreshPathAsync override blocks on Task.Delay so
        // the caller's cancellation hits AFTER BeginAsync has acquired the connection-scoped
        // GET_LOCK, AND BEFORE the base calls CommitAsync. The UoW is wrapped by a spy that
        // captures the token passed to RollbackAsync from the runner's catch path.
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new MySqlOutboxMigrationCatalog();

        var cancellingRunner = new CancellingMySqlBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(30));
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
        // the caller's signalled token. MySQL makes this assertion load-bearing: the explicit
        // RELEASE_LOCK call lives inside RollbackAsync (DDL auto-commits per ADR 0057 §5a so
        // there is no transaction-bound implicit release). If the catch had passed the cancelled
        // token, MySqlCommand would throw OCE before RELEASE_LOCK executed and GET_LOCK would
        // persist until the connection itself was closed.
        Assert.False(spy.RollbackToken.IsCancellationRequested,
            "RollbackAsync should receive CancellationToken.None to ensure RELEASE_LOCK completes.");
        Assert.Equal(CancellationToken.None, spy.RollbackToken);

        //Assert — the MySQL connection-scoped GET_LOCK IS released. A fresh runner whose
        // BeginAsync calls GET_LOCK on the same per-table lock resource completes the migration
        // normally; the 5s lock timeout would expire and surface as MigrationLockDeadlockException
        // if the lock were still held.
        var freshRunner = new MySqlBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(5));
        await freshRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint, CancellationToken.None);

        Assert.True(await TableExistsAsync(),
            "Subsequent migration should have created the outbox table after the lock was released.");
    }

    private async Task<bool> TableExistsAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName";
        command.Parameters.AddWithValue("@TableName", _tableName);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS `{_tableName}`";
            await dropTable.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}

file sealed class CancellingMySqlBoxMigrationRunner : MySqlBoxMigrationRunner
{
    public RollbackTokenCapturingUnitOfWork? LastUnitOfWork { get; private set; }

    public CancellingMySqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout)
        : base(catalog, configuration, lockTimeout)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<MySqlTransaction>> CreateUnitOfWorkAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
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
        MySqlConnection connection, MySqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
}

file sealed class RollbackTokenCapturingUnitOfWork : IAmAProvisioningUnitOfWork<MySqlTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<MySqlTransaction> _inner;

    public bool RollbackInvoked { get; private set; }
    public CancellationToken RollbackToken { get; private set; }

    public RollbackTokenCapturingUnitOfWork(IAmAProvisioningUnitOfWork<MySqlTransaction> inner) => _inner = inner;

    public MySqlTransaction? Transaction => _inner.Transaction;

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
