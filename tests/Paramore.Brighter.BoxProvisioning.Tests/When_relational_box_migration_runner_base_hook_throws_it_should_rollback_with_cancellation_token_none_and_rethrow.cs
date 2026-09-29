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
/// On any exception thrown by a hook between <c>BeginAsync</c> and <c>CommitAsync</c> the
/// runner base must call <c>uow.RollbackAsync(CancellationToken.None)</c> — NOT the
/// caller's token — and rethrow the original exception. The <c>CancellationToken.None</c>
/// detail is load-bearing per ADR 0058 §B.3: passing the caller's token would cause
/// <c>RollbackAsync</c> itself to throw <see cref="OperationCanceledException"/> and
/// abandon the unwind when the caller's token is signalled.
/// </summary>
/// <remarks>
/// Four <c>[Test]</c>s — one per hook between <c>BeginAsync</c> and <c>CommitAsync</c>
/// (<c>EnsureHistoryTableAsync</c>, <c>RunFreshPathAsync</c>, <c>RunBootstrapPathAsync</c>,
/// <c>RunNormalPathAsync</c>) — share an arrange/act/assert helper.
/// </remarks>
public class SqlBoxMigrationRunnerHookFailureTests
{
    [Test]
    public Task When_ensure_history_table_throws_runner_should_rollback_with_cancellation_token_none_and_rethrow()
        => AssertHookFailureRollsBackWithNoneAndRethrows(ThrowFromHook.EnsureHistoryTable);

    [Test]
    public Task When_run_fresh_path_throws_runner_should_rollback_with_cancellation_token_none_and_rethrow()
        => AssertHookFailureRollsBackWithNoneAndRethrows(ThrowFromHook.RunFreshPath);

    [Test]
    public Task When_run_bootstrap_path_throws_runner_should_rollback_with_cancellation_token_none_and_rethrow()
        => AssertHookFailureRollsBackWithNoneAndRethrows(ThrowFromHook.RunBootstrapPath);

    [Test]
    public Task When_run_normal_path_throws_runner_should_rollback_with_cancellation_token_none_and_rethrow()
        => AssertHookFailureRollsBackWithNoneAndRethrows(ThrowFromHook.RunNormalPath);

    private static async Task AssertHookFailureRollsBackWithNoneAndRethrows(ThrowFromHook hookToThrow)
    {
        //Arrange
        var unitOfWork = new CapturingProvisioningUnitOfWork();
        var runner = new ThrowingHookTestRunner(unitOfWork, hookToThrow);
        // The caller's token must be non-default — we are proving the runner does NOT
        // forward it to RollbackAsync. A fresh CancellationTokenSource produces a token
        // whose underlying source is non-null, so it is not equal to CancellationToken.None.
        using var cts = new CancellationTokenSource();
        await Assert.That(cts.Token).IsNotEqualTo(CancellationToken.None);

        //Act
        var thrown = await TestExceptionRecorder.CaptureAsync(() => runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0),
            cancellationToken: cts.Token));

        //Assert
        // The original sentinel exception must propagate to the caller — neither swallowed
        // nor wrapped — so callers can pattern-match on the type they actually threw.
        await Assert.That(thrown).IsTypeOf<InvalidOperationException>();
        await Assert.That(thrown!.Message).IsEqualTo(ThrowingHookTestRunner.SentinelMessage);

        // RollbackAsync must have been called (so the unwind ran) AND it must have been
        // called with CancellationToken.None — proving the runner did NOT forward the
        // caller's cts.Token.
        await Assert.That(unitOfWork.RollbackToken).IsNotNull();
        await Assert.That(unitOfWork.RollbackToken!.Value).IsEqualTo(CancellationToken.None);
    }

    private enum ThrowFromHook
    {
        EnsureHistoryTable,
        RunFreshPath,
        RunBootstrapPath,
        RunNormalPath
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> with <see cref="SentinelMessage"/>
    /// from one configured hook; the other hooks complete successfully. <c>RedetectStateAsync</c>
    /// drives which path-hook is reached when the throw target is one of the path hooks.
    /// </summary>
    private sealed class ThrowingHookTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public const string SentinelMessage = "spec 0028 Phase 6.2 sentinel — hook failure";

        private readonly CapturingProvisioningUnitOfWork _unitOfWork;
        private readonly ThrowFromHook _hookToThrow;

        public ThrowingHookTestRunner(CapturingProvisioningUnitOfWork unitOfWork, ThrowFromHook hookToThrow)
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog(),
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                NullLogger.Instance)
        {
            _unitOfWork = unitOfWork;
            _hookToThrow = hookToThrow;
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
        {
            if (_hookToThrow == ThrowFromHook.EnsureHistoryTable)
                throw new InvalidOperationException(SentinelMessage);
            return Task.CompletedTask;
        }

        protected override Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => _hookToThrow switch
            {
                // EnsureHistoryTable throws before this method is reached; default the result.
                ThrowFromHook.RunFreshPath => Task.FromResult((false, false)),
                ThrowFromHook.RunBootstrapPath => Task.FromResult((true, false)),
                ThrowFromHook.RunNormalPath => Task.FromResult((true, true)),
                _ => Task.FromResult((false, false))
            };

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
        {
            if (_hookToThrow == ThrowFromHook.RunFreshPath)
                throw new InvalidOperationException(SentinelMessage);
            return Task.CompletedTask;
        }

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        {
            if (_hookToThrow == ThrowFromHook.RunBootstrapPath)
                throw new InvalidOperationException(SentinelMessage);
            return Task.CompletedTask;
        }

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        {
            if (_hookToThrow == ThrowFromHook.RunNormalPath)
                throw new InvalidOperationException(SentinelMessage);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Captures the cancellation token passed to <see cref="RollbackAsync"/> so the test can
    /// assert it equals <see cref="CancellationToken.None"/>. The token is exposed as
    /// nullable so a never-called <see cref="RollbackAsync"/> is distinguishable from a
    /// "called with None" invocation.
    /// </summary>
    private sealed class CapturingProvisioningUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        public FakeDbTransaction? Transaction => null;
        public CancellationToken? RollbackToken { get; private set; }

        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            RollbackToken = cancellationToken;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => default;
    }
}
