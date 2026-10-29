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

/// <summary>
/// When <c>CommitAsync</c> throws after the transaction has completed, the runner still calls
/// <c>RollbackAsync</c>, which logs a Warning instead of throwing, and the caller sees the original
/// commit exception (ADR 0058 §B.3).
/// </summary>
public class MsSqlCommitThrowsBestEffortRollbackTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_mssql_commit_throws_rollback_should_be_best_effort_without_throwing()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var catalog = new MsSqlOutboxMigrationCatalog();
        var capturingLogger = new CapturingLogger();

        var commitFailure = new InvalidOperationException(
            "forced post-finalisation commit failure for spec 0028 Phase 10.3 contract test");

        var commitThrowingRunner = new CommitThrowingMsSqlBoxMigrationRunner(
            catalog, config, TimeSpan.FromSeconds(30), capturingLogger, commitFailure);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => commitThrowingRunner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        //Assert
        Assert.Same(commitFailure, thrown);

        var spy = commitThrowingRunner.LastUnitOfWork;
        Assert.NotNull(spy);
        Assert.True(spy!.RollbackInvoked,
            "The runner's catch-path MUST call RollbackAsync after CommitAsync throws (per ADR §B.3 best-effort unwind).");

        Assert.Contains(capturingLogger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Exception is InvalidOperationException
            && !ReferenceEquals(entry.Exception, commitFailure));

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
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        ILogger logger,
        Exception commitFailure)
        : base(catalog, configuration, lockTimeout, advisoryLock: null, logger: logger)
    {
        _commitFailure = commitFailure;
    }

    protected override async Task<IAmAProvisioningUnitOfWork<SqlTransaction>> CreateUnitOfWorkAsync(
        SqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        var inner = await base.CreateUnitOfWorkAsync(connection, schemaName, tableName, cancellationToken);
        LastUnitOfWork = new CommitFinalisingThrowingUnitOfWork(inner, _commitFailure);
        return LastUnitOfWork;
    }
}

// Commits the real transaction before throwing: a spy that only threw would leave it open, so
// RollbackAsync would succeed and the Warning path under test would never run.
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
        await _inner.CommitAsync(cancellationToken);
        throw _commitFailure;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackInvoked = true;
        return _inner.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
