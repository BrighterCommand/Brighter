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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// When the unit of work's <c>BeginAsync</c> throws (a lock timeout, or a failed <c>BeginTransaction</c>),
/// the runner must call neither <c>CommitAsync</c> nor <c>RollbackAsync</c>, must still dispose the
/// unit of work, and must let the original exception propagate.
/// </summary>
/// <remarks>
/// Pins <c>BeginAsync</c> outside the runner's try/catch (ADR 0058 §B.3): there is nothing to roll
/// back before the unit of work has begun, but <c>await using</c> still disposes it.
/// </remarks>
public class SqlBoxMigrationRunnerBeginFailureTests
{
    [Fact]
    public async Task When_begin_async_throws_runner_should_skip_commit_and_rollback_and_still_dispose()
    {
        //Arrange
        var sentinel = new InvalidOperationException("spec 0028 Phase 6.3 sentinel — BeginAsync failure");
        var unitOfWork = new CapturingProvisioningUnitOfWork(throwOnBegin: sentinel);
        var runner = new BeginFailureTestRunner(unitOfWork);
        using var cts = new CancellationTokenSource();

        //Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0),
            cancellationToken: cts.Token));

        //Assert
        Assert.Same(sentinel, thrown);

        Assert.True(unitOfWork.BeginAsyncCalled);

        Assert.False(unitOfWork.CommitAsyncCalled);

        Assert.False(unitOfWork.RollbackAsyncCalled);

        Assert.True(unitOfWork.DisposeAsyncCalled);
    }

    // Post-Begin hooks throw NotSupportedException, so reaching one surfaces as the wrong exception rather than the sentinel.
    private sealed class BeginFailureTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        private readonly CapturingProvisioningUnitOfWork _unitOfWork;

        public BeginFailureTestRunner(CapturingProvisioningUnitOfWork unitOfWork)
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog(),
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                NullLogger.Instance)
        {
            _unitOfWork = unitOfWork;
        }

        protected override string? DefaultHistorySchema => null;

        protected override Task<FakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new FakeDbConnection());

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
            => Task.FromResult<IAmAProvisioningUnitOfWork<FakeDbTransaction>>(_unitOfWork);

        protected override string LockResourceFor(string? schemaName, string tableName)
            => $"lock_{tableName}";

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("EnsureHistoryTableAsync must not be reached when BeginAsync throws.");

        protected override Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("RedetectStateAsync must not be reached when BeginAsync throws.");

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunFreshPathAsync must not be reached when BeginAsync throws.");

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunBootstrapPathAsync must not be reached when BeginAsync throws.");

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunNormalPathAsync must not be reached when BeginAsync throws.");
    }

    private sealed class CapturingProvisioningUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        private readonly Exception _throwOnBegin;

        public CapturingProvisioningUnitOfWork(Exception throwOnBegin)
        {
            _throwOnBegin = throwOnBegin;
        }

        public FakeDbTransaction? Transaction => null;

        public bool BeginAsyncCalled { get; private set; }
        public bool CommitAsyncCalled { get; private set; }
        public bool RollbackAsyncCalled { get; private set; }
        public bool DisposeAsyncCalled { get; private set; }

        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
        {
            BeginAsyncCalled = true;
            throw _throwOnBegin;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            CommitAsyncCalled = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            RollbackAsyncCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return default;
        }
    }
}
