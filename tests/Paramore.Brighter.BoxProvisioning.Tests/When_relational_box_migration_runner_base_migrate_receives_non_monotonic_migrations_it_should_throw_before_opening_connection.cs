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
/// The runner base must reject a non-monotonic migration list BEFORE opening any
/// connection, acquiring any advisory lock, or beginning any transaction. Invalid lists
/// corrupt every path branch (PK violation on history insert, skipped ALTERs, double-applied
/// DDL — see spec 0027 Items H/I/Q), so the validation sits at <see cref="MigrateAsync"/>
/// entry per ADR 0058 §B.2.
/// <para>
/// Spec 0027 R1 (PR #4039 part 2): the chain is sourced from the injected catalog, so a
/// malformed list is wired in via <see cref="StubBoxMigrationCatalog.Migrations"/> rather
/// than passed as a <c>MigrateAsync</c> argument.
/// </para>
/// <para>
/// This test pins the ordering by recording <see cref="OrderProbeTestRunner.OpenConnectionCalled"/>
/// inside the <see cref="OrderProbeTestRunner.OpenConnectionAsync"/> override. A non-monotonic
/// list must produce a <see cref="ConfigurationException"/> with that flag still <c>false</c>.
/// </para>
/// </summary>
public class SqlBoxMigrationRunnerMonotonicityValidationTests
{
    [Fact]
    public async Task When_migration_list_has_a_version_gap_migrate_should_throw_before_opening_connection()
    {
        //Arrange
        // Gap discriminator: V1 → V3 skips V2. ValidateMigrationsMonotonic enforces curr == prev + 1.
        var catalog = new StubBoxMigrationCatalog
        {
            Migrations = new IAmABoxMigration[]
            {
                new StubBoxMigration(version: 1),
                new StubBoxMigration(version: 3)
            }
        };
        var runner = new OrderProbeTestRunner(catalog);

        //Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Orders",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0)));

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.False(runner.OpenConnectionCalled);
    }

    /// <summary>
    /// Records whether <c>OpenConnectionAsync</c> was invoked. All other hooks throw
    /// <see cref="NotSupportedException"/> as defense-in-depth — the test asserts the
    /// validation throws BEFORE the connection opens, so no other hook should ever be
    /// reached. If the ordering regresses (e.g. validation moves to AFTER OpenConnection),
    /// either <see cref="OpenConnectionCalled"/> flips true or one of the post-Open hooks
    /// throws <see cref="NotSupportedException"/> instead of <see cref="ConfigurationException"/>.
    /// </summary>
    private sealed class OrderProbeTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public bool OpenConnectionCalled { get; private set; }

        public OrderProbeTestRunner(IAmABoxMigrationCatalog catalog)
            : base(
                new StubBoxDetectionHelper(),
                catalog,
                new StubRelationalDatabaseConfiguration(),
                TimeSpan.FromSeconds(30),
                NullLogger.Instance)
        {
        }

        protected override Task<FakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            OpenConnectionCalled = true;
            return Task.FromResult(new FakeDbConnection());
        }

        protected override Task<IAmAProvisioningUnitOfWork<FakeDbTransaction>> CreateUnitOfWorkAsync(
            FakeDbConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
            => throw new NotSupportedException("CreateUnitOfWorkAsync must not be reached when validation throws.");

        protected override string LockResourceFor(string? schemaName, string tableName)
            => throw new NotSupportedException("LockResourceFor must not be reached when validation throws.");

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("EnsureHistoryTableAsync must not be reached when validation throws.");

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunFreshPathAsync must not be reached when validation throws.");

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunBootstrapPathAsync must not be reached when validation throws.");

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunNormalPathAsync must not be reached when validation throws.");
    }

    private sealed class StubBoxMigration : IAmABoxMigration
    {
        public StubBoxMigration(int version)
        {
            Version = version;
        }

        public int Version { get; }
        public string Description => $"V{Version}";
        public string UpScript => string.Empty;
        public IReadOnlyCollection<string> LogicalColumns => Array.Empty<string>();
        public string? SourceReference => null;
        public string? IdempotencyCheckSql => null;
    }
}
