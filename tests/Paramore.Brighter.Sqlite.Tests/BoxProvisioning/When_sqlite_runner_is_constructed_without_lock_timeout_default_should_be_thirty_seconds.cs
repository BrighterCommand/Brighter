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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

/// <summary>
/// A runner constructed without a lockTimeout passes a 30-second default to the unit of work's
/// BeginAsync. The spy unit of work records it, then throws to stop the migration.
/// </summary>
public class RunnerDefaultLockTimeoutTests : IAsyncLifetime
{
    // Per-test DB file, so the test does not interact with sibling tests on test.db.
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_default_timeout_{Guid.NewGuid():N}.db");
    private readonly string _connectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    public RunnerDefaultLockTimeoutTests()
    {
        _connectionString = $"Data Source={_dbPath}";
    }

    [Fact]
    public async Task When_sqlite_runner_is_constructed_without_lock_timeout_default_should_be_thirty_seconds()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var capturingRunner = new TimeoutCapturingSqliteBoxMigrationRunner(config);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await Assert.ThrowsAsync<TimeoutException>(() => capturingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        //Assert
        var spy = capturingRunner.LastUnitOfWork;
        Assert.NotNull(spy);
        Assert.Equal(TimeSpan.FromSeconds(30), spy!.CapturedLockTimeout);
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

file sealed class TimeoutCapturingSqliteBoxMigrationRunner : SqliteBoxMigrationRunner
{
    public TimeoutCapturingSqliteUnitOfWork? LastUnitOfWork { get; private set; }

    // Omits lockTimeout: its default is what the test pins.
    public TimeoutCapturingSqliteBoxMigrationRunner(IAmARelationalDatabaseConfiguration configuration)
        : base(new SqliteBoxDetectionHelper(), new SqliteOutboxMigrationCatalog(), configuration)
    {
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqliteTransaction>> CreateUnitOfWorkAsync(
        SqliteConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, schemaName, tableName, cancellationToken);
        LastUnitOfWork = new TimeoutCapturingSqliteUnitOfWork(inner);
        return LastUnitOfWork;
    }
}

file sealed class TimeoutCapturingSqliteUnitOfWork : IAmAProvisioningUnitOfWork<SqliteTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<SqliteTransaction> _inner;

    public TimeSpan? CapturedLockTimeout { get; private set; }

    public TimeoutCapturingSqliteUnitOfWork(IAmAProvisioningUnitOfWork<SqliteTransaction> inner) => _inner = inner;

    public SqliteTransaction? Transaction => _inner.Transaction;

    // Throws before the inner BeginAsync, so no writer slot is taken.
    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        CapturedLockTimeout = lockTimeout;
        throw new TimeoutException("short-circuit to read captured timeout");
    }

    public Task CommitAsync(CancellationToken cancellationToken) => _inner.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) => _inner.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
