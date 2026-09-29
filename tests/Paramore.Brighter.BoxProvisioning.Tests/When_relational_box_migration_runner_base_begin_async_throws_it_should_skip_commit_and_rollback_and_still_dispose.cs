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

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// When the per-backend UoW's <c>BeginAsync</c> throws — e.g. advisory-lock acquisition
/// times out or the underlying <c>BeginTransaction</c> fails — the runner base must
/// neither call <c>CommitAsync</c> nor <c>RollbackAsync</c>, but the <c>await using</c>
/// declaration must still dispatch <c>DisposeAsync</c> on the UoW. The original
/// <c>BeginAsync</c> exception must propagate to the caller of <c>MigrateAsync</c>.
/// </summary>
/// <remarks>
/// Locks the structural placement of <c>BeginAsync</c> OUTSIDE the runner's try/catch
/// per ADR 0058 §B.3: the catch unwinds the BeginAsync→CommitAsync window only, so a
/// failure during BeginAsync itself must NOT trigger RollbackAsync (the UoW has nothing
/// to roll back) yet must still dispose (per the <c>await using</c> contract). A future
/// mutation that wrapped BeginAsync in the try would call RollbackAsync on an unbegun
/// UoW; this test would catch that regression.
/// </remarks>
public class SqlBoxMigrationRunnerBeginFailureTests
{
    [Test]
    public async Task When_begin_async_throws_runner_should_skip_commit_and_rollback_and_still_dispose()
    {
        //Arrange
        var sentinel = new InvalidOperationException("spec 0028 Phase 6.3 sentinel — BeginAsync failure");
        var unitOfWork = new CapturingProvisioningUnitOfWork(throwOnBegin: sentinel);
        var runner = new BeginFailureTestRunner(unitOfWork);
        using var cts = new CancellationTokenSource();

        //Act
        var thrown = await TestExceptionRecorder.CaptureAsync(() => runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0),
            cancellationToken: cts.Token));

        //Assert
        // The original sentinel from BeginAsync must propagate to the caller — neither
        // swallowed nor wrapped — proving no exception-substitution occurred during the
        // (skipped) post-Begin try/catch path.
        await Assert.That(thrown).IsSameReferenceAs(sentinel);

        // BeginAsync was reached so the runner did get past CreateUnitOfWorkAsync.
        await Assert.That(unitOfWork.BeginAsyncCalled).IsTrue();

        // CommitAsync MUST NOT be called — there is nothing to commit if Begin failed.
        await Assert.That(unitOfWork.CommitAsyncCalled).IsFalse();

        // RollbackAsync MUST NOT be called — there is nothing to roll back if Begin failed.
        // This is the load-bearing assertion: it pins BeginAsync as OUTSIDE the runner's
        // try/catch (ADR 0058 §B.3). A future mutation that moved BeginAsync inside the
        // try would call Rollback on an unbegun UoW and fail this assertion.
        await Assert.That(unitOfWork.RollbackAsyncCalled).IsFalse();

        // DisposeAsync MUST be called via `await using`, regardless of BeginAsync's throw.
        // This pins the runner's UoW declaration as `await using var uow = ...` (not a
        // bare assignment); a future mutation that dropped `await using` would skip
        // disposal and fail this assertion.
        await Assert.That(unitOfWork.DisposeAsyncCalled).IsTrue();
    }

    /// <summary>
    /// Minimal derivative of <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/>
    /// whose CreateUnitOfWorkAsync returns the supplied capturing spy; all post-Begin
    /// hooks throw <see cref="NotSupportedException"/> as defense-in-depth — if the
    /// runner ever called them despite Begin throwing, the test would observe the wrong
    /// exception type rather than the sentinel.
    /// </summary>
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

    /// <summary>
    /// UoW spy that throws <see cref="_throwOnBegin"/> from <see cref="BeginAsync"/>
    /// and records which lifecycle methods were invoked. Each flag is exposed so the
    /// test can pin "Begin reached, Commit/Rollback skipped, Dispose still ran".
    /// </summary>
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
