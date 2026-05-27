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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// Item #10 (PR #4039 third review). The IAmAProvisioningUnitOfWork contract says
/// <c>RollbackAsync</c> MUST NOT throw, and the four per-backend implementations comply (PG and
/// MySQL were tightened explicitly in <c>3c8417fd6</c>). Defense in depth: if a future regression
/// breaks that contract, the rollback exception MUST NOT mask the original triggering
/// exception. The original cause is more diagnostically valuable than a defect in our own
/// unwind, and existing callers pattern-match on the type they actually threw.
/// </summary>
public class SqlBoxMigrationRunnerRollbackFailureTests
{
    [Fact]
    public async Task When_rollback_async_throws_the_original_hook_exception_should_propagate_and_rollback_failure_should_be_logged()
    {
        // Arrange — EnsureHistoryTableAsync throws the PRIMARY sentinel; RollbackAsync throws the
        // ROLLBACK sentinel during the unwind. The runner is expected to swallow the ROLLBACK
        // sentinel (and log it) and rethrow the PRIMARY sentinel to the caller.
        var unitOfWork = new ThrowingRollbackUnitOfWork();
        var logger = new CapturingLogger<TestableRollbackFailureRunner>();
        var runner = new TestableRollbackFailureRunner(unitOfWork, logger);

        // Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0),
            cancellationToken: CancellationToken.None));

        // Assert — the PRIMARY sentinel reaches the caller. If the rollback exception was
        // allowed to escape, this would be an InvalidProgramException ("rollback sentinel"),
        // not the InvalidOperationException primary sentinel.
        Assert.IsType<InvalidOperationException>(thrown);
        Assert.Equal(TestableRollbackFailureRunner.PrimarySentinelMessage, thrown!.Message);

        // The rollback exception must have been logged (so operators can see that both a primary
        // failure AND a rollback failure happened — diagnosing one without the other would be
        // misleading). Log level should be Error since RollbackAsync violating its no-throw
        // contract is a defect in our own unwind, not a routine condition.
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Error
                 && e.Exception is InvalidProgramException
                 && e.Exception.Message == ThrowingRollbackUnitOfWork.RollbackSentinelMessage);
    }

    private sealed class TestableRollbackFailureRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public const string PrimarySentinelMessage = "PR #4039 item #10 primary sentinel — hook failure";

        private readonly ThrowingRollbackUnitOfWork _unitOfWork;

        public TestableRollbackFailureRunner(
            ThrowingRollbackUnitOfWork unitOfWork,
            ILogger<TestableRollbackFailureRunner> logger)
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog(),
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                logger)
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
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException(PrimarySentinelMessage);

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>
    /// A UoW that violates its no-throw RollbackAsync contract. Used distinct exception types
    /// for the primary sentinel (<see cref="InvalidOperationException"/>) and the rollback
    /// sentinel (<see cref="InvalidProgramException"/>) so the test can tell which one
    /// propagated by type rather than by message-matching.
    /// </summary>
    private sealed class ThrowingRollbackUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        public const string RollbackSentinelMessage = "PR #4039 item #10 rollback sentinel — should not propagate";

        public FakeDbTransaction? Transaction => null;

        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken)
            => throw new InvalidProgramException(RollbackSentinelMessage);

        public ValueTask DisposeAsync() => default;
    }
}
