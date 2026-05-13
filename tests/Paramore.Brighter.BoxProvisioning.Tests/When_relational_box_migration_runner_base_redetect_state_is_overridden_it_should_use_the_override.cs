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
/// The third escape hatch documented in ADR 0058 §B.2: a derived runner whose backend has
/// a non-standard detection model (e.g. a backend that combines the
/// <c>DoesTableExistAsync</c>/<c>DoesHistoryExistAsync</c> probes into a single round-trip)
/// can override <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}.RedetectStateAsync"/>
/// without overriding any other hook. This test pins the contract by arranging a runner
/// whose override returns a tuple inconsistent with what the injected detection helper
/// would otherwise produce: the override drives <c>RunNormalPath</c> dispatch even though
/// the helper's <c>TableExistsResult</c>/<c>HistoryExistsResult</c> are both false.
/// Asserts:
///   1. The override is invoked (<see cref="OverrideTestRunner.RedetectOverrideCallCount"/> == 1).
///   2. The dispatch matches the override's return (<c>PathInvoked == "RunNormalPath"</c>).
///   3. The injected detection helper is NOT touched (call counts both zero) — proves the
///      base default was completely bypassed by the override.
/// </summary>
public class SqlBoxMigrationRunnerRedetectStateOverrideTests
{
    [Fact]
    public async Task When_redetect_state_is_overridden_migrate_should_use_the_override_and_bypass_the_default()
    {
        //Arrange
        var detectionHelper = new StubBoxDetectionHelper
        {
            // Override-bypass discriminator: the helper would say (false, false) → fresh path,
            // but the override returns (true, true) → normal path. The dispatch we observe
            // proves which one MigrateAsync used.
            TableExistsResult = false,
            HistoryExistsResult = false
        };
        var runner = new OverrideTestRunner(detectionHelper);

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            migrations: Array.Empty<IAmABoxMigration>(),
            tableState: new BoxTableState(false, false, 0));

        //Assert
        Assert.Equal(1, runner.RedetectOverrideCallCount);
        Assert.Equal("RunNormalPath", runner.PathInvoked);
        Assert.Equal(0, detectionHelper.DoesTableExistCallCount);
        Assert.Equal(0, detectionHelper.DoesHistoryExistCallCount);
    }

    /// <summary>
    /// Overrides <c>RedetectStateAsync</c> to return constant <c>(true, true)</c> so the
    /// dispatched branch is <c>RunNormalPath</c> regardless of the injected helper's stub
    /// values. No other hook is overridden beyond the abstract minimum required to compile.
    /// </summary>
    private sealed class OverrideTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public int RedetectOverrideCallCount { get; private set; }
        public string? PathInvoked { get; private set; }

        public OverrideTestRunner(StubBoxDetectionHelper detectionHelper)
            : base(
                detectionHelper,
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                NullLogger.Instance)
        {
        }

        protected override Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
        {
            RedetectOverrideCallCount++;
            return Task.FromResult((tableExists: true, historyExists: true));
        }

        protected override Task<FakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new FakeDbConnection());

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, CancellationToken cancellationToken)
            => Task.FromResult<IAmAProvisioningUnitOfWork<FakeDbTransaction>>(new NoOpProvisioningUnitOfWork());

        protected override string LockResourceFor(string? schemaName, string tableName)
            => $"lock_{tableName}";

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
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
