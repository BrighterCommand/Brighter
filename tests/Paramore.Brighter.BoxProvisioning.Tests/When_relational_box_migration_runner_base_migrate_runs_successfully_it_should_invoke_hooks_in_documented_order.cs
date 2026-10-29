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
/// The runner must call its hooks in a fixed order on the success path, so derived classes have a
/// stable contract to extend (ADR 0058 §B.2). Each test pins the order on one path: fresh, bootstrap
/// or normal.
/// </summary>
public class SqlBoxMigrationRunnerHookOrderTests
{
    [Fact]
    public async Task When_table_does_not_exist_migrate_should_invoke_hooks_in_fresh_path_order()
    {
        //Arrange
        var unitOfWork = new RecordingProvisioningUnitOfWork();
        var runner = new RecordingTestRunner(unitOfWork)
        {
            TableExistsForRedetect = false,
            HistoryExistsForRedetect = false
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0));

        //Assert
        Assert.Equal(
            new[]
            {
                "OpenConnection",
                "CreateUnitOfWork",
                "BeginAsync",
                "EnsureHistoryTable",
                "RedetectState",
                "RunFreshPath",
                "CommitAsync",
                "DisposeAsync"
            },
            unitOfWork.Log);
    }

    [Fact]
    public async Task When_table_exists_without_history_migrate_should_invoke_hooks_in_bootstrap_path_order()
    {
        //Arrange
        var unitOfWork = new RecordingProvisioningUnitOfWork();
        var runner = new RecordingTestRunner(unitOfWork)
        {
            TableExistsForRedetect = true,
            HistoryExistsForRedetect = false
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(true, false, 0));

        //Assert
        Assert.Equal(
            new[]
            {
                "OpenConnection",
                "CreateUnitOfWork",
                "BeginAsync",
                "EnsureHistoryTable",
                "RedetectState",
                "RunBootstrapPath",
                "CommitAsync",
                "DisposeAsync"
            },
            unitOfWork.Log);
    }

    [Fact]
    public async Task When_table_exists_with_history_migrate_should_invoke_hooks_in_normal_path_order()
    {
        //Arrange
        var unitOfWork = new RecordingProvisioningUnitOfWork();
        var runner = new RecordingTestRunner(unitOfWork)
        {
            TableExistsForRedetect = true,
            HistoryExistsForRedetect = true
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(true, true, 0));

        //Assert
        Assert.Equal(
            new[]
            {
                "OpenConnection",
                "CreateUnitOfWork",
                "BeginAsync",
                "EnsureHistoryTable",
                "RedetectState",
                "RunNormalPath",
                "CommitAsync",
                "DisposeAsync"
            },
            unitOfWork.Log);
    }

    // Overrides RedetectStateAsync so the order under test does not depend on the detection helper.
    private sealed class RecordingTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        private readonly RecordingProvisioningUnitOfWork _unitOfWork;

        public bool TableExistsForRedetect { get; set; }
        public bool HistoryExistsForRedetect { get; set; }

        public RecordingTestRunner(RecordingProvisioningUnitOfWork unitOfWork)
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
        {
            _unitOfWork.Log.Add("OpenConnection");
            return Task.FromResult(new FakeDbConnection());
        }

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
        {
            _unitOfWork.Log.Add("CreateUnitOfWork");
            return Task.FromResult<IAmAProvisioningUnitOfWork<FakeDbTransaction>>(_unitOfWork);
        }

        protected override string LockResourceFor(string? schemaName, string tableName)
            => $"lock_{tableName}";

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
        {
            _unitOfWork.Log.Add("EnsureHistoryTable");
            return Task.CompletedTask;
        }

        protected override Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
        {
            _unitOfWork.Log.Add("RedetectState");
            return Task.FromResult((TableExistsForRedetect, HistoryExistsForRedetect));
        }

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
        {
            _unitOfWork.Log.Add("RunFreshPath");
            return Task.CompletedTask;
        }

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        {
            _unitOfWork.Log.Add("RunBootstrapPath");
            return Task.CompletedTask;
        }

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
        {
            _unitOfWork.Log.Add("RunNormalPath");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProvisioningUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        public List<string> Log { get; } = new();
        public FakeDbTransaction? Transaction => null;

        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
        {
            Log.Add("BeginAsync");
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Log.Add("CommitAsync");
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            Log.Add("RollbackAsync");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Log.Add("DisposeAsync");
            return default;
        }
    }
}
