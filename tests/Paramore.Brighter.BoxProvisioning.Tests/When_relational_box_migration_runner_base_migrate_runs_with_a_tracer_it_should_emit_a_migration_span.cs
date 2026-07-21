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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// Box migration is a startup-critical step and previously emitted nothing observable —
/// operators had to correlate scattered logs to see which path (fresh / bootstrap / normal)
/// ran or whether the history table was created cleanly. This test pins the contract that,
/// when a tracer is wired into the runner, <c>MigrateAsync</c> emits a single migration span
/// per call carrying backend / table / box-type / chosen-path tags AND one child event per
/// branch taken so the trace tells the full story on its own.
/// </summary>
[NotInParallel]
public class SqlBoxMigrationRunnerObservabilityTests : IDisposable
{
    private readonly List<Activity> _exportedActivities = new();
    private readonly TracerProvider _tracerProvider;
    private readonly BrighterTracer _tracer;

    public SqlBoxMigrationRunnerObservabilityTests()
    {
        // Subscribe to the standard "Paramore.Brighter" source so migration spans land in the
        // same trace stream operators already use for outbox / publish / dispatch spans. The
        // "Paramore.Brighter.Tests" source is also added so the parent-activity test can pin
        // the child-parent relationship without a separate ActivityListener (whose sampling
        // decision would race the TracerProvider's).
        _tracer = new BrighterTracer();
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(BrighterSemanticConventions.SourceName, "Paramore.Brighter.Tests")
            .AddInMemoryExporter(_exportedActivities)
            .Build()!;
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
        _tracer.Dispose();
    }

    [Test]
    public async Task When_migrate_async_runs_on_the_fresh_path_it_should_emit_a_span_with_ensure_history_and_fresh_install_events()
    {
        //Arrange
        var unitOfWork = new ObservabilityTestUnitOfWork();
        var runner = new ObservabilityTestRunner(unitOfWork, _tracer)
        {
            TableExistsForRedetect = false,
            HistoryExistsForRedetect = false
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: "dbo",
            boxType: BoxType.Outbox,
            tableState:new BoxTableState(false, false, 0));

        _tracerProvider.ForceFlush();

        //Assert — exactly one migration span was exported with the expected name shape.
        var span = await Assert.That(_exportedActivities).HasSingleItem();
        await Assert.That(span.DisplayName).IsEqualTo($"{BrighterSemanticConventions.BoxMigration} Orders");

        //Assert — span carries the operational tags an operator needs to disambiguate which
        //migration this was: backend, schema, table, box type, and the path chosen after
        //under-lock re-detection.
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.DbSystem && (string?)t.Value == DbSystem.OtherSql.ToDbName())).IsTrue();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.DbTable && (string?)t.Value == "Orders")).IsTrue();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.DbNamespace && (string?)t.Value == "dbo")).IsTrue();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.BoxType && (string?)t.Value == BoxType.Outbox.ToString())).IsTrue();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.BoxMigrationPath && (string?)t.Value == "fresh")).IsTrue();

        //Assert — child events fired in the documented order: ensure_history_table first,
        //then fresh_install (the path-taken event).
        var eventNames = span.Events.Select(e => e.Name).ToArray();
        await Assert.That(eventNames).IsEquivalentTo(new[]
            {
                BrighterSemanticConventions.BoxMigrationEventEnsureHistory,
                BrighterSemanticConventions.BoxMigrationEventFreshInstall
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task When_migrate_async_runs_on_the_bootstrap_path_it_should_emit_a_span_with_ensure_history_and_bootstrap_events()
    {
        //Arrange
        var unitOfWork = new ObservabilityTestUnitOfWork();
        var runner = new ObservabilityTestRunner(unitOfWork, _tracer)
        {
            TableExistsForRedetect = true,
            HistoryExistsForRedetect = false
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Inbox,
            tableState:new BoxTableState(true, false, 0));

        _tracerProvider.ForceFlush();

        //Assert
        var span = await Assert.That(_exportedActivities).HasSingleItem();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.BoxMigrationPath && (string?)t.Value == "bootstrap")).IsTrue();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.BoxType && (string?)t.Value == BoxType.Inbox.ToString())).IsTrue();
        //schemaName=null → namespace tag is omitted (SQLite has no schema concept; suppressing
        //the tag keeps it from polluting traces with a meaningless 'null' literal).
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.DbNamespace)).IsFalse();

        var eventNames = span.Events.Select(e => e.Name).ToArray();
        await Assert.That(eventNames).IsEquivalentTo(new[]
            {
                BrighterSemanticConventions.BoxMigrationEventEnsureHistory,
                BrighterSemanticConventions.BoxMigrationEventBootstrap
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task When_migrate_async_runs_on_the_normal_path_it_should_emit_a_span_with_ensure_history_and_normal_update_events()
    {
        //Arrange
        var unitOfWork = new ObservabilityTestUnitOfWork();
        var runner = new ObservabilityTestRunner(unitOfWork, _tracer)
        {
            TableExistsForRedetect = true,
            HistoryExistsForRedetect = true
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: "dbo",
            boxType: BoxType.Outbox,
            tableState:new BoxTableState(true, true, 7));

        _tracerProvider.ForceFlush();

        //Assert
        var span = await Assert.That(_exportedActivities).HasSingleItem();
        await Assert.That((span.TagObjects).Any(t => t.Key == BrighterSemanticConventions.BoxMigrationPath && (string?)t.Value == "normal")).IsTrue();

        var eventNames = span.Events.Select(e => e.Name).ToArray();
        await Assert.That(eventNames).IsEquivalentTo(new[]
            {
                BrighterSemanticConventions.BoxMigrationEventEnsureHistory,
                BrighterSemanticConventions.BoxMigrationEventNormalUpdate
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task When_migrate_async_runs_under_a_parent_activity_it_should_emit_a_child_span()
    {
        //Arrange — establish a parent activity so the test can assert the migration span links
        //to it (operators correlating traces care that 'migrate' nests under whatever startup
        //task scheduled it, e.g. BoxProvisioningHostedService).
        using var parentSource = new ActivitySource("Paramore.Brighter.Tests");
        using var parentActivity = parentSource.StartActivity("test-parent")!;

        var unitOfWork = new ObservabilityTestUnitOfWork();
        var runner = new ObservabilityTestRunner(unitOfWork, _tracer)
        {
            TableExistsForRedetect = false,
            HistoryExistsForRedetect = false
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState:new BoxTableState(false, false, 0));

        _tracerProvider.ForceFlush();

        //Assert — both parent and migration span exported; migration is a direct child.
        var migrationSpan = await Assert.That(_exportedActivities).HasSingleItem();
        await Assert.That(migrationSpan.ParentSpanId.ToString()).IsEqualTo(parentActivity.SpanId.ToString());
    }

    [Test]
    public async Task When_no_tracer_is_supplied_migrate_async_should_complete_without_emitting_a_span()
    {
        //Arrange — null tracer is the default; existing call-sites that haven't opted in to
        //instrumentation must not start paying for spans they didn't ask for.
        var unitOfWork = new ObservabilityTestUnitOfWork();
        var runner = new ObservabilityTestRunner(unitOfWork, tracer: null)
        {
            TableExistsForRedetect = false,
            HistoryExistsForRedetect = false
        };

        //Act
        await runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState:new BoxTableState(false, false, 0));

        _tracerProvider.ForceFlush();

        //Assert
        await Assert.That(_exportedActivities).IsEmpty();
    }

    private sealed class ObservabilityTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        private readonly ObservabilityTestUnitOfWork _unitOfWork;

        public bool TableExistsForRedetect { get; set; }
        public bool HistoryExistsForRedetect { get; set; }

        public ObservabilityTestRunner(ObservabilityTestUnitOfWork unitOfWork, IAmABrighterTracer? tracer)
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog(),
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                logger: null,
                tracer: tracer)
        {
            _unitOfWork = unitOfWork;
        }

        //OtherSql is the fall-back enum value for a generic SQL backend — concrete backends
        //(MSSQL/PG/MySQL/SQLite) override with their specific DbSystem in production code.
        protected override DbSystem DbSystem => DbSystem.OtherSql;

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
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => Task.FromResult((TableExistsForRedetect, HistoryExistsForRedetect));

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

    private sealed class ObservabilityTestUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        public FakeDbTransaction? Transaction => null;
        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
    }
}
