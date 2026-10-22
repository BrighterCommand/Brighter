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
/// <c>IAmAProvisioningUnitOfWork.RollbackAsync</c> must not throw, but if one does, the runner must log it
/// at Error and rethrow the original exception: the original cause is the one worth diagnosing, and
/// callers match on the type they threw.
/// </summary>
public class SqlBoxMigrationRunnerRollbackFailureTests
{
    [Fact]
    public async Task When_rollback_async_throws_the_original_hook_exception_should_propagate_and_rollback_failure_should_be_logged()
    {
        //Arrange
        var unitOfWork = new ThrowingRollbackUnitOfWork();
        var logger = new CapturingLogger<TestableRollbackFailureRunner>();
        var runner = new TestableRollbackFailureRunner(unitOfWork, logger);

        //Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0),
            cancellationToken: CancellationToken.None));

        //Assert
        Assert.IsType<InvalidOperationException>(thrown);
        Assert.Equal(TestableRollbackFailureRunner.PrimarySentinelMessage, thrown!.Message);

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
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
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

    // Throws InvalidProgramException, a different type from the primary sentinel, so the test can tell which one propagated.
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
