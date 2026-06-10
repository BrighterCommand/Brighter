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
/// <see cref="SqlBoxMigrationRunner{TConnection, TTransaction}.MigrateAsync"/> is the framework
/// chokepoint for the runner. Catalogs already gate <see cref="Identifiers.AssertSafe"/> on
/// <c>configuration.OutBoxTableName</c> / <c>SchemaName</c> at the entry to <c>All</c> — but
/// the runner accepts <paramref name="tableName"/> and <paramref name="schemaName"/> as
/// parameters, so a host that bypasses the catalog (or supplies a forged value to a direct
/// runner call) must still be gated at the framework boundary. This test pins
/// defence-in-depth: an unsafe identifier must fail with <see cref="ConfigurationException"/>
/// BEFORE <c>OpenConnectionAsync</c> is invoked, mirroring the existing
/// <c>ValidateMigrationsMonotonic</c> ordering contract at §B.2.
/// </summary>
public class SqlBoxMigrationRunnerIdentifierValidationTests
{
    [Fact]
    public async Task When_migrate_table_name_is_unsafe_it_should_throw_before_opening_connection()
    {
        //Arrange
        var runner = new IdentifierProbeTestRunner();

        //Act: classic injection vector — quoted identifier with embedded statement.
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Outbox; DROP TABLE x",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0)));

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.False(runner.OpenConnectionCalled);
    }

    [Fact]
    public async Task When_migrate_schema_name_is_unsafe_it_should_throw_before_opening_connection()
    {
        //Arrange
        var runner = new IdentifierProbeTestRunner();

        //Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Outbox",
            schemaName: "dbo; --",
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0)));

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.False(runner.OpenConnectionCalled);
    }

    [Fact]
    public async Task When_migrate_schema_name_is_null_it_should_not_throw_for_identifier_validation()
    {
        //Arrange: SQLite has no schema concept (ADR 0057 §6) — a null schemaName must pass
        //         identifier validation. Pinning this prevents an over-eager AssertSafe(null)
        //         from regressing the SQLite path.
        var runner = new IdentifierProbeTestRunner();

        //Act
        var thrown = await Record.ExceptionAsync(() => runner.MigrateAsync(
            tableName: "Outbox",
            schemaName: null,
            boxType: BoxType.Outbox,
            tableState: new BoxTableState(false, false, 0)));

        //Assert: not a ConfigurationException about identifiers — the runner gets past the
        //        identifier guard and reaches OpenConnectionAsync, where the probe's
        //        FakeDbConnection returns. Subsequent hooks throw NotSupportedException; the
        //        first one reached confirms identifier validation did NOT reject null schema.
        Assert.IsNotType<ConfigurationException>(thrown);
        Assert.True(runner.OpenConnectionCalled);
    }

    /// <summary>
    /// Records whether <c>OpenConnectionAsync</c> was invoked. The identifier guard must fire
    /// BEFORE this hook — if validation regresses to AFTER OpenConnection, either
    /// <see cref="OpenConnectionCalled"/> flips true or one of the post-Open hooks throws
    /// <see cref="NotSupportedException"/> instead of <see cref="ConfigurationException"/>.
    /// Mirrors the sibling probe at
    /// <c>SqlBoxMigrationRunnerMonotonicityValidationTests.OrderProbeTestRunner</c>.
    /// </summary>
    private sealed class IdentifierProbeTestRunner : SqlBoxMigrationRunner<FakeDbConnection, FakeDbTransaction>
    {
        public bool OpenConnectionCalled { get; private set; }

        public IdentifierProbeTestRunner()
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog
                {
                    Migrations = new IAmABoxMigration[] { new StubBoxMigration(version: 1) }
                },
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
            => throw new NotSupportedException("CreateUnitOfWorkAsync must not be reached when identifier validation throws.");

        protected override string LockResourceFor(string? schemaName, string tableName)
            => throw new NotSupportedException("LockResourceFor must not be reached when identifier validation throws.");

        protected override Task EnsureHistoryTableAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("EnsureHistoryTableAsync must not be reached when identifier validation throws.");

        protected override Task RunFreshPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunFreshPathAsync must not be reached when identifier validation throws.");

        protected override Task RunBootstrapPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunBootstrapPathAsync must not be reached when identifier validation throws.");

        protected override Task RunNormalPathAsync(
            FakeDbConnection connection, FakeDbTransaction? transaction, string? schemaName, string tableName,
            IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
            => throw new NotSupportedException("RunNormalPathAsync must not be reached when identifier validation throws.");
    }

    private sealed class StubBoxMigration : IAmABoxMigration
    {
        public StubBoxMigration(int version)
        {
            Version = version;
        }

        public MigrationVersion Version { get; }
        public MigrationDescription Description => $"V{Version}";
        public SqlScript UpScript => string.Empty;
        public IReadOnlyCollection<string> LogicalColumns => Array.Empty<string>();
        public SourceReference? SourceReference => null;
        public SqlScript? IdempotencyCheckSql => null;
    }
}
