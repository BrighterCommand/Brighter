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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// The abstract base <c>SqlBoxProvisioner&lt;TConnection, TTransaction&gt;</c> must orchestrate
/// its hooks (connection lifecycle, detection helper, payload validator, migration runner) in a
/// documented, fixed order on each of the three table-state branches so derived classes have a
/// stable contract to extend (per ADR 0058 §B.5). This test pins the order on each branch — table
/// exists with history (normal path), table exists without history (bootstrap path), table missing
/// (fresh path) — by recording the sequence of hook invocations into a shared list. Mirrors the
/// §B.2 sibling-base hook-order test at
/// <c>When_relational_box_migration_runner_base_migrate_runs_successfully_it_should_invoke_hooks_in_documented_order</c>.
/// </summary>
public class SqlBoxProvisionerHookOrderTests
{
    [Test]
    public async Task When_table_exists_with_history_it_should_call_GetMaxVersion_then_validate_payload_then_migrate()
    {
        //Arrange
        var log = new List<string>();
        var detection = new RecordingDetectionHelper(log)
        {
            TableExistsResult = true,
            HistoryExistsResult = true,
            MaxVersionResult = 5
        };
        var payloadValidator = new RecordingPayloadValidator(log);
        var migrationRunner = new RecordingMigrationRunner(log);
        var catalog = new RecordingMigrationCatalog();
        var configuration = new RecordingConfiguration();
        var provisioner = new TestSqlBoxProvisioner(
            detection, catalog, payloadValidator, configuration, migrationRunner, BoxType.Outbox, log);

        //Act
        await provisioner.ProvisionAsync();

        //Assert: normal-path order — detection takes the GetMaxVersion branch (history exists),
        //        payload validation runs on the SAME connection (item #11: prior shape opened a
        //        second connection here and closed both back-to-back), then MigrateAsync.
        await Assert.That(log).IsEquivalentTo(new[]
            {
                "CreateConnection",
                "OpenAsync",
                "DoesTableExistAsync",
                "DoesHistoryExistAsync",
                "GetMaxVersionAsync",
                "ValidateAsync",
                "Dispose",
                "MigrateAsync"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(migrationRunner.CapturedTableState).IsNotNull();
        await Assert.That(migrationRunner.CapturedTableState!.TableExists).IsTrue();
        await Assert.That(migrationRunner.CapturedTableState!.HistoryExists).IsTrue();
        await Assert.That(migrationRunner.CapturedTableState!.CurrentVersion).IsEqualTo(5);
    }

    [Test]
    public async Task When_table_exists_without_history_it_should_take_bootstrap_branch_then_validate_payload_then_migrate()
    {
        //Arrange
        var log = new List<string>();
        var detection = new RecordingDetectionHelper(log)
        {
            TableExistsResult = true,
            HistoryExistsResult = false,
            DetectedVersionResult = 2
        };
        var payloadValidator = new RecordingPayloadValidator(log);
        var migrationRunner = new RecordingMigrationRunner(log);
        var catalog = new RecordingMigrationCatalog();
        var configuration = new RecordingConfiguration();
        var provisioner = new TestSqlBoxProvisioner(
            detection, catalog, payloadValidator, configuration, migrationRunner, BoxType.Outbox, log);

        //Act
        await provisioner.ProvisionAsync();

        //Assert: bootstrap-path order — detection takes the DetectCurrentVersion branch
        //        (table present, no history); payload validation runs on the SAME connection
        //        (item #11: prior shape opened a second connection here), then MigrateAsync.
        await Assert.That(log).IsEquivalentTo(new[]
            {
                "CreateConnection",
                "OpenAsync",
                "DoesTableExistAsync",
                "DoesHistoryExistAsync",
                "DetectCurrentVersionAsync",
                "ValidateAsync",
                "Dispose",
                "MigrateAsync"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(migrationRunner.CapturedTableState).IsNotNull();
        await Assert.That(migrationRunner.CapturedTableState!.TableExists).IsTrue();
        await Assert.That(migrationRunner.CapturedTableState!.HistoryExists).IsFalse();
    }

    [Test]
    public async Task When_table_does_not_exist_it_should_skip_history_and_payload_checks_and_call_migrate_with_fresh_state()
    {
        //Arrange
        var log = new List<string>();
        var detection = new RecordingDetectionHelper(log)
        {
            TableExistsResult = false
        };
        var payloadValidator = new RecordingPayloadValidator(log);
        var migrationRunner = new RecordingMigrationRunner(log);
        var catalog = new RecordingMigrationCatalog();
        var configuration = new RecordingConfiguration();
        var provisioner = new TestSqlBoxProvisioner(
            detection, catalog, payloadValidator, configuration, migrationRunner, BoxType.Outbox, log);

        //Act
        await provisioner.ProvisionAsync();

        //Assert: fresh-path order — DoesTableExistAsync returns false, so DoesHistoryExistAsync
        //        is NOT called, payload validation is NOT called, and MigrateAsync is invoked
        //        directly with TableExists: false / HistoryExists: false / CurrentVersion: 0.
        await Assert.That(log).IsEquivalentTo(new[]
            {
                "CreateConnection",
                "OpenAsync",
                "DoesTableExistAsync",
                "Dispose",
                "MigrateAsync"
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(migrationRunner.CapturedTableState).IsNotNull();
        await Assert.That(migrationRunner.CapturedTableState!.TableExists).IsFalse();
        await Assert.That(migrationRunner.CapturedTableState!.HistoryExists).IsFalse();
        await Assert.That(migrationRunner.CapturedTableState!.CurrentVersion).IsEqualTo(0);
    }

    /// <summary>
    /// Hard-codes <c>CreateConnection</c> (returns a recording <see cref="FakeDbConnection"/> that
    /// logs <c>OpenAsync</c> and <c>Dispose</c>) and <c>PayloadColumnName</c> (<c>"Body"</c> — the
    /// Outbox convention; the test does not exercise the casing decision per se). The base hosts
    /// the orchestration; this test derivative supplies only the abstract hooks.
    /// </summary>
    private sealed class TestSqlBoxProvisioner : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        private readonly List<string> _log;

        public TestSqlBoxProvisioner(
            IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction> detectionHelper,
            IAmABoxMigrationCatalog catalog,
            IAmABoxPayloadModeValidator<FakeDbConnection> payloadValidator,
            IAmARelationalDatabaseConfiguration configuration,
            IAmABoxMigrationRunner migrationRunner,
            BoxType boxType,
            List<string> log)
            : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, boxType)
        {
            _log = log;
        }

        protected override FakeDbConnection CreateConnection(string connectionString)
        {
            _log.Add("CreateConnection");
            return new RecordingFakeDbConnection(_log);
        }

        protected override string PayloadColumnName => "Body";
    }

    /// <summary>
    /// Subclass of the existing <see cref="FakeDbConnection"/> test double that records the
    /// <c>OpenAsync</c> and <c>Dispose</c> calls into the shared log. <c>Dispose</c> is recorded
    /// via the <see cref="System.Data.Common.DbConnection.Dispose(bool)"/> override (the abstract
    /// <c>DbConnection.Dispose(bool)</c> does NOT call <c>Close()</c> by default — concrete ADO.NET
    /// providers override one or the other to wire them together); the sync <c>using</c> contract
    /// in <c>SqlBoxProvisioner</c> routes through this code path.
    /// </summary>
    private sealed class RecordingFakeDbConnection : FakeDbConnection
    {
        private readonly List<string> _log;

        public RecordingFakeDbConnection(List<string> log)
        {
            _log = log;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _log.Add("OpenAsync");
            return base.OpenAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _log.Add("Dispose");
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Records each detection-helper call into the shared log so the assertion can pin the call
    /// order across the three branches. <see cref="GetMaxVersionAsync"/> and
    /// <see cref="DetectCurrentVersionAsync"/> return configured values; the other members are
    /// not exercised by these tests and throw if reached.
    /// </summary>
    private sealed class RecordingDetectionHelper : IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction>
    {
        private readonly List<string> _log;

        public RecordingDetectionHelper(List<string> log)
        {
            _log = log;
        }

        public bool TableExistsResult { get; set; }
        public bool HistoryExistsResult { get; set; }
        public int MaxVersionResult { get; set; }
        public int DetectedVersionResult { get; set; }

        public Task<bool> DoesTableExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            _log.Add("DoesTableExistAsync");
            return Task.FromResult(TableExistsResult);
        }

        public Task<bool> DoesHistoryExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName, string? historySchema,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            _log.Add("DoesHistoryExistAsync");
            return Task.FromResult(HistoryExistsResult);
        }

        public Task<int> GetMaxVersionAsync(
            FakeDbConnection connection, string tableName, string? schemaName, string? historySchema,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            _log.Add("GetMaxVersionAsync");
            return Task.FromResult(MaxVersionResult);
        }

        public Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
            => throw new System.NotSupportedException();

        public string DiscriminatorFor(BoxType boxType)
            => throw new System.NotSupportedException();

        public Task<int> DetectCurrentVersionAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            _log.Add("DetectCurrentVersionAsync");
            return Task.FromResult(DetectedVersionResult);
        }
    }

    private sealed class RecordingPayloadValidator : IAmABoxPayloadModeValidator<FakeDbConnection>
    {
        private readonly List<string> _log;

        public RecordingPayloadValidator(List<string> log)
        {
            _log = log;
        }

        public Task ValidateAsync(
            FakeDbConnection connection,
            string tableName,
            string? schemaName,
            string columnName,
            bool binaryMessagePayload,
            CancellationToken cancellationToken = default)
        {
            _log.Add("ValidateAsync");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMigrationRunner : IAmABoxMigrationRunner
    {
        private readonly List<string> _log;

        public RecordingMigrationRunner(List<string> log)
        {
            _log = log;
        }

        public BoxTableState? CapturedTableState { get; private set; }

        public Task MigrateAsync(
            BoxTableName tableName,
            SchemaName? schemaName,
            BoxType boxType,
            BoxTableState tableState,
            CancellationToken cancellationToken = default)
        {
            _log.Add("MigrateAsync");
            CapturedTableState = tableState;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMigrationCatalog : IAmABoxMigrationCatalog
    {
        public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
            => System.Array.Empty<IAmABoxMigration>();

        public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
            => string.Empty;
    }

    /// <summary>
    /// Returns a non-null schema name (<c>"dbo"</c>) so the test exercises the schema-propagation
    /// path. Slice 1's implementation hard-codes <c>_configuration.SchemaName</c>; slice 2 extracts
    /// the <c>EffectiveSchemaName</c> virtual hook and slice 3 adds <c>ClampDetectedVersion</c> —
    /// neither is needed here, so the recording configuration only differs from the existing
    /// <see cref="StubRelationalDatabaseConfiguration"/> in returning a non-null schema name and
    /// non-empty box-table names so the runner-call argument capture is meaningful.
    /// </summary>
    private sealed class RecordingConfiguration : IAmARelationalDatabaseConfiguration
    {
        public bool BinaryMessagePayload => false;
        public bool JsonMessagePayload => false;
        public string ConnectionString => "Server=fake;Database=fake;";
        public string DatabaseName => "fake";
        public string InBoxTableName => "Commands";
        public string OutBoxTableName => "Messages";
        public string QueueStoreTable => "Queues";
        public string? SchemaName => "dbo";
    }
}
