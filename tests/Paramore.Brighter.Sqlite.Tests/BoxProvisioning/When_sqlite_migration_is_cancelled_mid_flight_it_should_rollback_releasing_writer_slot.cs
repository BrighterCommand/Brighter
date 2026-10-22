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

/// <summary>
/// Cancelling a migration after BEGIN IMMEDIATE but before commit rolls back with
/// <see cref="CancellationToken.None"/>, so SQLite's writer slot is released (ADR 0058 §B.3).
/// </summary>
public class MigrationCancellationRollbackTests : IAsyncLifetime
{
    // Per-test DB file, so the writer slot held here does not contend with sibling tests on test.db.
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_cancel_{Guid.NewGuid():N}.db");
    private readonly string _connectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    public MigrationCancellationRollbackTests()
    {
        _connectionString = $"Data Source={_dbPath}";
    }

    [Fact]
    public async Task When_sqlite_migration_is_cancelled_mid_flight_it_should_rollback_releasing_writer_slot()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new SqliteOutboxMigrationCatalog();

        var cancellingRunner = new CancellingSqliteBoxMigrationRunner(catalog, config);
        var staleHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(250));

        //Act
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancellingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint, cts.Token));

        //Assert
        Assert.NotNull(thrown);

        var spy = cancellingRunner.LastUnitOfWork;
        Assert.NotNull(spy);
        Assert.True(spy!.RollbackInvoked,
            "RollbackAsync should have been invoked after the mid-flight cancellation per ADR §B.3.");

        Assert.False(spy.RollbackToken.IsCancellationRequested,
            "RollbackAsync should receive CancellationToken.None to ensure the writer-slot release completes.");
        Assert.Equal(CancellationToken.None, spy.RollbackToken);

        var freshRunner = new SqliteBoxMigrationRunner(catalog, config, TimeSpan.FromSeconds(5));
        await freshRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, staleHint, CancellationToken.None);

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

    public CancellingSqliteBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration)
        : base(catalog, configuration)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqliteTransaction>> CreateUnitOfWorkAsync(
        SqliteConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, schemaName, tableName, cancellationToken);
        LastUnitOfWork = new RollbackTokenCapturingUnitOfWork(inner);
        return LastUnitOfWork;
    }

    // Task.Delay's TaskCanceledException is an OperationCanceledException, so it takes the base runner's catch path.
    protected override Task RunFreshPathAsync(
        SqliteConnection connection, SqliteTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
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
