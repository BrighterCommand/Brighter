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
/// The base class supplies a default <c>RedetectStateAsync</c> implementation that calls
/// the injected detection helper and short-circuits the history check when the table is
/// missing (per ADR 0058 §B.2). This test exercises the base default — the runner derivative
/// here does NOT override <c>RedetectStateAsync</c> — across the three detection-result
/// combinations and asserts:
///   1. <c>DoesTableExistAsync = false</c> → <c>DoesHistoryExistAsync</c> NOT called → returns <c>(false, false)</c>.
///   2. <c>DoesTableExistAsync = true, DoesHistoryExistAsync = true</c> → returns <c>(true, true)</c>.
///   3. <c>DoesTableExistAsync = true, DoesHistoryExistAsync = false</c> → returns <c>(true, false)</c>.
/// The return value is observed indirectly by which dispatch path <c>MigrateAsync</c> selects:
/// (false, _) → fresh, (true, false) → bootstrap, (true, true) → normal. Combined with the
/// <see cref="StubBoxDetectionHelper.DoesHistoryExistCallCount"/> counter, this pins both the
/// short-circuit (Fact 1) and the return value (all three Facts).
/// </summary>
public class SqlBoxMigrationRunnerRedetectStateDefaultTests
{
    [Fact]
    public async Task When_table_does_not_exist_default_redetect_should_skip_history_check_and_return_false_false()
    {
        //Arrange
        var detectionHelper = new StubBoxDetectionHelper
        {
            // Short-circuit discriminator: table missing → default impl MUST NOT call DoesHistoryExistAsync.
            TableExistsResult = false,
            HistoryExistsResult = false
        };
        var runner = new PathRecordingTestRunner(detectionHelper);

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0));

        //Assert
        Assert.Equal(1, detectionHelper.DoesTableExistCallCount);
        Assert.Equal(0, detectionHelper.DoesHistoryExistCallCount);
        Assert.Equal("RunFreshPath", runner.PathInvoked);
    }

    [Fact]
    public async Task When_table_exists_with_history_default_redetect_should_call_both_and_return_true_true()
    {
        //Arrange
        var detectionHelper = new StubBoxDetectionHelper
        {
            // Normal-path discriminator: table AND history both present → returns (true, true).
            TableExistsResult = true,
            HistoryExistsResult = true
        };
        var runner = new PathRecordingTestRunner(detectionHelper);

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(true, true, 0));

        //Assert
        Assert.Equal(1, detectionHelper.DoesTableExistCallCount);
        Assert.Equal(1, detectionHelper.DoesHistoryExistCallCount);
        Assert.Equal("RunNormalPath", runner.PathInvoked);
    }

    [Fact]
    public async Task When_table_exists_without_history_default_redetect_should_call_both_and_return_true_false()
    {
        //Arrange
        var detectionHelper = new StubBoxDetectionHelper
        {
            // Bootstrap-path discriminator: legacy table present, no history rows → returns (true, false).
            TableExistsResult = true,
            HistoryExistsResult = false
        };
        var runner = new PathRecordingTestRunner(detectionHelper);

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(true, false, 0));

        //Assert
        Assert.Equal(1, detectionHelper.DoesTableExistCallCount);
        Assert.Equal(1, detectionHelper.DoesHistoryExistCallCount);
        Assert.Equal("RunBootstrapPath", runner.PathInvoked);
    }

    /// <summary>
    /// Exercises the base's default <c>RedetectStateAsync</c> — the override is intentionally
    /// absent. Path-dispatch is recorded into <see cref="PathInvoked"/> so the test can observe
    /// the value the default implementation returned via the branch <c>MigrateAsync</c> took.
    /// </summary>
    private sealed class PathRecordingTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public string? PathInvoked { get; private set; }

        public PathRecordingTestRunner(StubBoxDetectionHelper detectionHelper)
            : base(
                detectionHelper,
                new StubBoxMigrationCatalog(),
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                NullLogger.Instance)
        {
        }

        protected override string? DefaultHistorySchema => null;

        protected override Task<FakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new FakeDbConnection());

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
            => Task.FromResult<IAmAProvisioningUnitOfWork<FakeDbTransaction>>(new NoOpProvisioningUnitOfWork());

        protected override string LockResourceFor(string? schemaName, string tableName)
            => $"lock_{tableName}";

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
        {
            PathInvoked = "RunFreshPath";
            return Task.CompletedTask;
        }

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        {
            PathInvoked = "RunBootstrapPath";
            return Task.CompletedTask;
        }

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        {
            PathInvoked = "RunNormalPath";
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpProvisioningUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        public FakeDbTransaction? Transaction => null;
        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
    }
}
