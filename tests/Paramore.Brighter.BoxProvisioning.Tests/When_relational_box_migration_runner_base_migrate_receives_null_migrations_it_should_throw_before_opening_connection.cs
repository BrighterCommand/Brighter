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
/// A catalog whose <see cref="IAmABoxMigrationCatalog.All"/> returns null at the runner's
/// framework chokepoint must surface as <see cref="ConfigurationException"/> with a
/// descriptive message BEFORE any connection is opened. The legacy failure mode was an
/// opaque NRE inside
/// <see cref="SqlBoxMigrationRunner{TConnection, TTransaction}.ValidateMigrationsMonotonic"/>
/// (<c>migrations[i - 1].Version</c>) or, on an empty list that snuck past, a misleading
/// <c>EnsureHistoryTableAsync</c> call against the live DB. Defence at the boundary is one
/// line and replaces both with a clear operator-facing diagnostic.
/// <para>
/// Spec 0027 R1 (PR #4039 part 2): the runner sources the chain from the injected catalog
/// rather than via a <c>MigrateAsync</c> argument; this test wires a null-returning catalog
/// via <see cref="StubBoxMigrationCatalog.AllReturnsNull"/>.
/// </para>
/// <para>
/// The empty-list case is intentionally NOT guarded — relational catalogs always return
/// at least a V1 migration, and many internal tests deliberately use an empty
/// <see cref="StubBoxMigrationCatalog.Migrations"/> as a "don't care" payload when
/// exercising hook-ordering, re-detection, or failure-path contracts. Adding an empty-list
/// guard would churn 8+ tests for no operational gain (a misconfigured catalog already
/// surfaces in catalog-construction tests).
/// </para>
/// </summary>
public class SqlBoxMigrationRunnerNullMigrationsValidationTests
{
    [Fact]
    public async Task When_catalog_returns_null_migrate_should_throw_configuration_exception_before_opening_connection()
    {
        //Arrange
        var catalog = new StubBoxMigrationCatalog { AllReturnsNull = true };
        var runner = new NullMigrationsProbeTestRunner(catalog);

        //Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Outbox",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0)));

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.False(runner.OpenConnectionCalled);
    }

    /// <summary>
    /// Records whether <c>OpenConnectionAsync</c> was invoked. Null-migrations validation
    /// must fire BEFORE the connection opens — if validation regresses to AFTER
    /// <c>OpenConnectionAsync</c>, either <see cref="OpenConnectionCalled"/> flips true or
    /// one of the post-Open hooks throws <see cref="NotSupportedException"/> instead of
    /// <see cref="ConfigurationException"/>.
    /// </summary>
    private sealed class NullMigrationsProbeTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public bool OpenConnectionCalled { get; private set; }

        public NullMigrationsProbeTestRunner(IAmABoxMigrationCatalog catalog)
            : base(
                new StubBoxDetectionHelper(),
                catalog,
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                NullLogger.Instance)
        {
        }

        protected override string? DefaultHistorySchema => null;

        protected override Task<FakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            OpenConnectionCalled = true;
            return Task.FromResult(new FakeDbConnection());
        }

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
            => throw new NotSupportedException("CreateUnitOfWorkAsync must not be reached when null-migrations validation throws.");

        protected override string LockResourceFor(string? schemaName, string tableName)
            => throw new NotSupportedException("LockResourceFor must not be reached when null-migrations validation throws.");

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("EnsureHistoryTableAsync must not be reached when null-migrations validation throws.");

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunFreshPathAsync must not be reached when null-migrations validation throws.");

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunBootstrapPathAsync must not be reached when null-migrations validation throws.");

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunNormalPathAsync must not be reached when null-migrations validation throws.");
    }
}
