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
/// A catalog whose <see cref="IAmABoxMigrationCatalog.All"/> returns null must fail with a descriptive
/// <see cref="ConfigurationException"/> before any connection is opened, rather than with an opaque
/// <c>NullReferenceException</c> later.
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

    // Hooks after OpenConnectionAsync throw NotSupportedException, so a validation that runs too late surfaces as the wrong exception.
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
