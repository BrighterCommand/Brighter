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

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class RunnerDefaultLockTimeoutTests
{
    // Per-test DB file so the test does not interact with siblings running against the shared
    // test.db (same isolation pattern as the cancellation test).
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"brighter_sqlite_default_timeout_{Guid.NewGuid():N}.db");
    private readonly string _connectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    public RunnerDefaultLockTimeoutTests()
    {
        _connectionString = $"Data Source={_dbPath}";
    }

    [Test]
    public async Task When_sqlite_runner_is_constructed_without_lock_timeout_default_should_be_thirty_seconds()
    {
        //Arrange — the runner is constructed via the detection-helper ctor with `lockTimeout`
        // OMITTED, exercising the optional-parameter default path. SQLite has no advisory-lock
        // abstraction (BEGIN IMMEDIATE takes the writer slot directly), so the observable
        // capture point is the IAmAProvisioningUnitOfWork.BeginAsync(lockTimeout: ...)
        // parameter. The spy wraps the real SQLite UoW, records the timeout, then throws to
        // short-circuit the migration.
        //
        // This test is a regression pin: SqliteBoxMigrationRunner.cs:82 already defaults to
        // TimeSpan.FromSeconds(30). The cross-backend Fix #1 commits (`acff5eb34`,
        // `ba8813e6f`, `080e93c96`) harmonised MSSQL / MySql / PostgreSql to match. Without
        // this pin, a future change that reverts SQLite to `?? default` would re-introduce the
        // divergence silently.
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var capturingRunner = new TimeoutCapturingSqliteBoxMigrationRunner(config);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await Assert.ThrowsAsync<TimeoutException>(() => capturingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        //Assert — the omitted `lockTimeout` resolves to the 30-second default flowed into the
        // UoW's BeginAsync(lockTimeout: ...) call. SqliteBoxMigrationRunner is the reference
        // value the other three relational runners are now harmonised against.
        var spy = capturingRunner.LastUnitOfWork;
        await Assert.That(spy).IsNotNull();
        await Assert.That(spy!.CapturedLockTimeout).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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

    // Uses the detection-helper ctor with `lockTimeout` OMITTED — the path under regression-pin.
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

    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        CapturedLockTimeout = lockTimeout;
        // Short-circuit: throw before the inner BeginAsync runs so no SQLite writer slot is
        // taken and no DB state is touched. The runner's catch-path then unwinds without a
        // transaction to roll back (DisposeAsync still runs on the inner UoW).
        throw new TimeoutException("short-circuit to read captured timeout");
    }

    public Task CommitAsync(CancellationToken cancellationToken) => _inner.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) => _inner.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}