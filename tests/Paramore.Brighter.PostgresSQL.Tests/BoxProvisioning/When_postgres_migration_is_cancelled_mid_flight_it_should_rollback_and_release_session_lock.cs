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
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlMigrationCancellationTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_postgres_migration_is_cancelled_mid_flight_it_should_rollback_and_release_session_lock()
    {
        //Arrange — fresh database; the cancelling runner's RunFreshPathAsync override blocks on
        // Task.Delay so the caller's cancellation hits AFTER BeginAsync has acquired the
        // session-scoped pg_advisory_lock and opened the NpgsqlTransaction, AND BEFORE the base
        // calls CommitAsync. The UoW is wrapped by a spy that captures the token passed to
        // RollbackAsync from the runner's catch path.
        new PostgresSqlTestHelper().SetupDatabase();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new PostgreSqlOutboxMigrationCatalog();

        var cancellingRunner = new CancellingPostgreSqlBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(30));
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
        // the caller's signalled token. Postgres makes this assertion load-bearing: the explicit
        // pg_advisory_unlock call lives inside RollbackAsync. If the catch had passed the
        // cancelled token, NpgsqlCommand would throw OCE before pg_advisory_unlock executed and
        // the session-scoped lock would persist until the connection itself was closed.
        Assert.False(spy.RollbackToken.IsCancellationRequested,
            "RollbackAsync should receive CancellationToken.None to ensure pg_advisory_unlock completes.");
        Assert.Equal(CancellationToken.None, spy.RollbackToken);

        //Assert — the Postgres session-scoped advisory lock IS released. A fresh runner whose
        // BeginAsync calls pg_advisory_lock on the same per-table lock resource completes the
        // migration normally; the 5s lock timeout would expire and surface as
        // MigrationLockDeadlockException if the lock were still held.
        var freshRunner = new PostgreSqlBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(5));
        await freshRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint, CancellationToken.None);

        Assert.True(await TableExistsAsync(),
            "Subsequent migration should have created the outbox table after the lock was released.");
    }

    private async Task<bool> TableExistsAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'public' AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $@"DROP TABLE IF EXISTS ""{_tableName}""";
            await dropTable.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DELETE FROM ""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}

file sealed class CancellingPostgreSqlBoxMigrationRunner : PostgreSqlBoxMigrationRunner
{
    public RollbackTokenCapturingUnitOfWork? LastUnitOfWork { get; private set; }

    public CancellingPostgreSqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout)
        : base(catalog, configuration, lockTimeout)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<NpgsqlTransaction>> CreateUnitOfWorkAsync(
        NpgsqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, schemaName, tableName, cancellationToken);
        LastUnitOfWork = new RollbackTokenCapturingUnitOfWork(inner);
        return LastUnitOfWork;
    }

    // Substitute fresh-path "work" with a delay long enough to be interrupted mid-flight by
    // the test's CancellationTokenSource.CancelAfter. The throw from Task.Delay is a
    // TaskCanceledException (subtype of OperationCanceledException) and is caught by the base
    // runner's catch path — which is the contract under test.
    protected override Task RunFreshPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
}

file sealed class RollbackTokenCapturingUnitOfWork : IAmAProvisioningUnitOfWork<NpgsqlTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<NpgsqlTransaction> _inner;

    public bool RollbackInvoked { get; private set; }
    public CancellationToken RollbackToken { get; private set; }

    public RollbackTokenCapturingUnitOfWork(IAmAProvisioningUnitOfWork<NpgsqlTransaction> inner) => _inner = inner;

    public NpgsqlTransaction? Transaction => _inner.Transaction;

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
