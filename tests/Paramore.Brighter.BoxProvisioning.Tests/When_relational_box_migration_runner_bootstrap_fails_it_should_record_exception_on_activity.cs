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
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// The runner starts its migration activity *before* opening a connection, creating
/// the unit-of-work, or calling <c>BeginAsync</c> — and the existing pre-W2 code did
/// those three calls outside the instrumentation try/catch. A connection-refused, a
/// UoW factory throw, or a lock-timeout from <c>BeginAsync</c> (per ADR 0058 §B.3)
/// therefore escaped with <see cref="ActivityStatusCode.Unset"/> and no recorded
/// exception event. Operators tracing a "migration never ran" story saw an OK-ish
/// span and had to fall back to scattered logs.
/// <para/>
/// These tests pin the contract that bootstrap failures land on the migration span:
/// status = Error AND the thrown exception is attached as an event. One [Fact] per
/// bootstrap call site so the regression points are individually addressable.
/// </summary>
[Collection("BoxProvisioningObservability")]
public class SqlBoxMigrationRunnerBootstrapFailureObservabilityTests : IDisposable
{
    private readonly List<Activity> _exportedActivities = new();
    private readonly TracerProvider _tracerProvider;
    private readonly BrighterTracer _tracer;

    public SqlBoxMigrationRunnerBootstrapFailureObservabilityTests()
    {
        _tracer = new BrighterTracer();
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(BrighterSemanticConventions.SourceName)
            .AddInMemoryExporter(_exportedActivities)
            .Build()!;
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
        _tracer.Dispose();
    }

    [Fact]
    public async Task When_open_connection_async_throws_it_should_record_error_status_and_exception_on_activity()
    {
        //Arrange — OpenConnectionAsync is the first bootstrap call after StartMigrationActivity.
        //A connection-refused or DNS failure must not produce an Unset/OK span.
        var thrown = new InvalidOperationException("connection refused (test)");
        var runner = new ThrowingBootstrapRunner(_tracer, openConnectionThrow: thrown);

        //Act — runner.MigrateAsync should rethrow the bootstrap exception …
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.MigrateAsync(
                tableName: "Orders",
                schemaName: "dbo",
                boxType: BoxType.Outbox,
                tableState: new BoxTableState(false, false, 0)));
        Assert.Same(thrown, caught);

        _tracerProvider.ForceFlush();

        //Assert — exactly one span exported, status = Error, exception recorded as an event.
        var span = Assert.Single(_exportedActivities);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Contains(span.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task When_create_unit_of_work_async_throws_it_should_record_error_status_and_exception_on_activity()
    {
        //Arrange — CreateUnitOfWorkAsync runs after OpenConnectionAsync. A factory failure
        //(eg. provider mismatch, transient pool exhaustion) must surface on the span.
        var thrown = new InvalidOperationException("UoW factory failed (test)");
        var runner = new ThrowingBootstrapRunner(_tracer, createUnitOfWorkThrow: thrown);

        //Act
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.MigrateAsync(
                tableName: "Orders",
                schemaName: "dbo",
                boxType: BoxType.Outbox,
                tableState: new BoxTableState(false, false, 0)));
        Assert.Same(thrown, caught);

        _tracerProvider.ForceFlush();

        //Assert
        var span = Assert.Single(_exportedActivities);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Contains(span.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task When_begin_async_throws_it_should_record_error_status_and_exception_on_activity()
    {
        //Arrange — BeginAsync is documented in ADR 0058 §B.3 as the lock-acquisition step:
        //a lock-timeout MUST be observable on the span, not just in logs.
        var thrown = new TimeoutException("advisory lock timeout (test)");
        var runner = new ThrowingBootstrapRunner(_tracer, beginAsyncThrow: thrown);

        //Act
        var caught = await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.MigrateAsync(
                tableName: "Orders",
                schemaName: "dbo",
                boxType: BoxType.Outbox,
                tableState: new BoxTableState(false, false, 0)));
        Assert.Same(thrown, caught);

        _tracerProvider.ForceFlush();

        //Assert
        var span = Assert.Single(_exportedActivities);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Contains(span.Events, e => e.Name == "exception");
    }

    private sealed class ThrowingBootstrapRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        private readonly Exception? _openConnectionThrow;
        private readonly Exception? _createUnitOfWorkThrow;
        private readonly Exception? _beginAsyncThrow;

        public ThrowingBootstrapRunner(
            IAmABrighterTracer tracer,
            Exception? openConnectionThrow = null,
            Exception? createUnitOfWorkThrow = null,
            Exception? beginAsyncThrow = null)
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog(),
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                logger: null,
                tracer: tracer)
        {
            _openConnectionThrow = openConnectionThrow;
            _createUnitOfWorkThrow = createUnitOfWorkThrow;
            _beginAsyncThrow = beginAsyncThrow;
        }

        protected override DbSystem DbSystem => DbSystem.OtherSql;

        protected override Task<FakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            if (_openConnectionThrow is not null) throw _openConnectionThrow;
            return Task.FromResult(new FakeDbConnection());
        }

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, CancellationToken cancellationToken)
        {
            if (_createUnitOfWorkThrow is not null) throw _createUnitOfWorkThrow;
            return Task.FromResult<IAmAProvisioningUnitOfWork<FakeDbTransaction>>(
                new ThrowOnBeginUnitOfWork(_beginAsyncThrow));
        }

        protected override string LockResourceFor(string? schemaName, string tableName) => $"lock_{tableName}";

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName,
            CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken) => Task.FromResult((false, false));

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowOnBeginUnitOfWork : IAmAProvisioningUnitOfWork<FakeDbTransaction>
    {
        private readonly Exception? _beginThrow;
        public ThrowOnBeginUnitOfWork(Exception? beginThrow) => _beginThrow = beginThrow;

        public FakeDbTransaction? Transaction => null;
        public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
            => _beginThrow is not null ? Task.FromException(_beginThrow) : Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
    }
}
