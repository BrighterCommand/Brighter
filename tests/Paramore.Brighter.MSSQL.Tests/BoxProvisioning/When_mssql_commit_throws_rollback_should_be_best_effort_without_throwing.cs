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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_commit_throws_rollback_should_be_best_effort_without_throwing : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_propagate_commit_exception_unwrapped_and_log_warning_after_best_effort_rollback()
    {
        //Arrange — wire a runner whose UoW spy first commits the inner real SqlTransaction
        // (transitioning it to "completed" — indistinguishable from the post-failed-commit
        // zombied state per ADR 0058 §B.3) and THEN throws an InvalidOperationException
        // simulating a client-side commit failure after server-side completion (e.g. broken
        // connection mid-commit). The capturing logger is threaded into the runner so the
        // real MsSqlProvisioningUnitOfWork.RollbackAsync — which the spy delegates to from
        // the runner's catch-path — emits its Warning into a buffer the test can assert on.
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);
        var capturingLogger = new CapturingLogger();

        var commitFailure = new InvalidOperationException(
            "forced post-finalisation commit failure for spec 0028 Phase 10.3 contract test");

        var commitThrowingRunner = new CommitThrowingMsSqlBoxMigrationRunner(
            config, TimeSpan.FromSeconds(30), capturingLogger, commitFailure);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — runner enters the try-block, succeeds through DDL, then the spy's CommitAsync
        // throws after committing the inner real tx. The runner's catch-path calls
        // uow.RollbackAsync(CancellationToken.None) → spy → real UoW, where the underlying
        // SqlTransaction.RollbackAsync throws InvalidOperationException ("This SqlTransaction
        // has completed; it is no longer usable.") because the inner tx was committed by the
        // spy. The real UoW catches that, logs a Warning, and returns cleanly. The catch-block's
        // `throw;` then rethrows the original commitFailure — NOT the swallowed rollback IOE.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => commitThrowingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, freshHint));

        //Assert — the SAME instance of commitFailure surfaces to the caller (per ADR §B.3).
        // If a different IOE were rethrown, this assertion catches that — for example, if the
        // runner's catch-path RollbackAsync threw and replaced the original exception (which
        // would happen if the UoW didn't swallow the finalised-tx IOE), Assert.Same would fail
        // even though Assert.ThrowsAsync<InvalidOperationException> would still pass.
        Assert.Same(commitFailure, thrown);

        //Assert — the spy observed the runner's catch-path actually call RollbackAsync. This
        // pins "RollbackAsync runs" — the contract under test. Without this, a regression where
        // the runner skipped Rollback after a thrown Commit would still pass the warning and
        // exception-identity assertions only by coincidence (it wouldn't log the Warning, but
        // the test author might miss why).
        var spy = commitThrowingRunner.LastUnitOfWork;
        Assert.NotNull(spy);
        Assert.True(spy!.RollbackInvoked,
            "The runner's catch-path MUST call RollbackAsync after CommitAsync throws (per ADR §B.3 best-effort unwind).");

        //Assert — a Warning was logged by MsSqlProvisioningUnitOfWork.RollbackAsync's catch
        // block (the only Warning emitted on the box-provisioning code path). The captured
        // exception is the rollback's swallowed InvalidOperationException ("transaction
        // completed; no longer usable") — distinct from commitFailure by reference, since the
        // rollback IOE is constructed by the SqlTransaction itself, not by the test.
        Assert.Contains(capturingLogger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Exception is InvalidOperationException
            && !ReferenceEquals(entry.Exception, commitFailure));

        //Assert — the inner real CommitAsync DID succeed before the spy threw, so the migration
        // was actually applied to the database despite MigrateAsync throwing. This proves the
        // spy correctly produced the "zombied" state (a no-op spy that just threw without
        // committing wouldn't transition the SqlTransaction to "completed", and RollbackAsync
        // would succeed cleanly without logging the Warning — the test would silently
        // characterise the wrong scenario).
        Assert.True(TableExists(),
            "The spy must commit the inner transaction before throwing — otherwise the SqlTransaction is not in the finalised state and the contract being tested is not actually exercised.");
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

file sealed class CommitThrowingMsSqlBoxMigrationRunner : MsSqlBoxMigrationRunner
{
    private readonly Exception _commitFailure;

    public CommitFinalisingThrowingUnitOfWork? LastUnitOfWork { get; private set; }

    public CommitThrowingMsSqlBoxMigrationRunner(
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        ILogger logger,
        Exception commitFailure)
        : base(configuration, lockTimeout, advisoryLock: null, logger: logger)
    {
        _commitFailure = commitFailure;
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, cancellationToken);
        LastUnitOfWork = new CommitFinalisingThrowingUnitOfWork(inner, _commitFailure);
        return LastUnitOfWork;
    }
}

file sealed class CommitFinalisingThrowingUnitOfWork : IAmAProvisioningUnitOfWork<SqlTransaction>
{
    private readonly IAmAProvisioningUnitOfWork<SqlTransaction> _inner;
    private readonly Exception _commitFailure;

    public bool RollbackInvoked { get; private set; }

    public CommitFinalisingThrowingUnitOfWork(
        IAmAProvisioningUnitOfWork<SqlTransaction> inner, Exception commitFailure)
    {
        _inner = inner;
        _commitFailure = commitFailure;
    }

    public SqlTransaction? Transaction => _inner.Transaction;

    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
        => _inner.BeginAsync(lockResource, lockTimeout, cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        // Step 1 — actually commit the inner real SqlTransaction. This transitions it to the
        // "completed" state externally indistinguishable from a post-failed-commit zombied
        // transaction (e.g. mid-commit connection drop where the server committed but the
        // client never received the ACK).
        await _inner.CommitAsync(cancellationToken);

        // Step 2 — throw the configured commit failure. The runner sees CommitAsync as having
        // thrown; it enters its catch-path and calls RollbackAsync. Because the inner tx is
        // now finalised, the underlying SqlTransaction.RollbackAsync will throw IOE, which
        // the real MsSqlProvisioningUnitOfWork is contracted to swallow + Warning-log.
        throw _commitFailure;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackInvoked = true;
        return _inner.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
