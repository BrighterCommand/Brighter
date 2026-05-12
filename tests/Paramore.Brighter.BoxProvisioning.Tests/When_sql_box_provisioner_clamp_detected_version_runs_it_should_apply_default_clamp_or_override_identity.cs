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
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// The <c>SqlBoxProvisioner</c> base hosts <c>ClampDetectedVersion</c> as a transitional virtual
/// hook with a clamp-negative-to-zero default. Three of four relational backends inherit the
/// default; MySQL's identity override (added in 13.A.4) preserves its no-clamp behaviour bit-for-
/// bit through Phase 13.A. Phase 13.B (F11) removes both the MySQL override AND this hook in one
/// commit, inlining the clamp into <c>DetectTableStateAsync</c>. These tests pin the default-clamp
/// contract and the identity-override extension point (the latter is the MySQL shape that 13.A.4
/// will adopt and 13.B will delete).
/// </summary>
/// <remarks>
/// Each <c>[Fact]</c> exercises the bootstrap branch (table exists, no history) — the only call
/// site where <c>ClampDetectedVersion</c> applies. The history-exists branch goes through
/// <c>GetMaxVersionAsync</c> and does NOT apply clamp.
/// </remarks>
public class SqlBoxProvisionerClampDetectedVersionTests
{
    [Fact]
    public async Task When_clamp_detected_version_default_sees_negative_it_should_return_zero()
    {
        //Arrange — default derivation (no override); DetectCurrentVersionAsync returns -1
        //(spec 0027's "discriminator missing" sentinel). The clamp default maps -1 → 0.
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = -1 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisionerWithDefaultClamp(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — captured BoxTableState.CurrentVersion clamped to 0.
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(0, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    [Fact]
    public async Task When_clamp_detected_version_default_sees_positive_it_should_pass_through_unchanged()
    {
        //Arrange — default derivation; DetectCurrentVersionAsync returns 3 (a real V_k).
        //The clamp default leaves non-negatives untouched.
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = 3 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisionerWithDefaultClamp(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — captured BoxTableState.CurrentVersion preserves 3.
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(3, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    [Fact]
    public async Task When_clamp_detected_version_is_overridden_to_identity_it_should_propagate_negative_unchanged()
    {
        //Arrange — identity-override derivation (the MySQL-during-13.A shape; removed in 13.B);
        //DetectCurrentVersionAsync returns -1. The override bypasses the clamp.
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = -1 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisionerWithIdentityClamp(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — captured BoxTableState.CurrentVersion is -1 (unchanged from detected value).
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(-1, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    /// <summary>
    /// Derivation with no <c>ClampDetectedVersion</c> override — inherits the base's clamp default
    /// (negative → 0; non-negative pass-through). Represents the MSSQL/PostgreSQL/SQLite shape.
    /// </summary>
    private sealed class TestSqlBoxProvisionerWithDefaultClamp : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public TestSqlBoxProvisionerWithDefaultClamp(
            IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction> detectionHelper,
            IAmABoxMigrationCatalog catalog,
            IAmABoxPayloadModeValidator<FakeDbConnection> payloadValidator,
            IAmARelationalDatabaseConfiguration configuration,
            IAmABoxMigrationRunner migrationRunner,
            BoxType boxType)
            : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, boxType)
        {
        }

        protected override FakeDbConnection CreateConnection(string connectionString)
            => new FakeDbConnection();

        protected override string PayloadColumnName => "Body";
    }

    /// <summary>
    /// Derivation that overrides <c>ClampDetectedVersion</c> to identity — represents the MySQL
    /// transitional shape during Phase 13.A (the override is removed in Phase 13.B alongside the
    /// base hook itself; clamp is then inlined into <c>DetectTableStateAsync</c>).
    /// </summary>
    private sealed class TestSqlBoxProvisionerWithIdentityClamp : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public TestSqlBoxProvisionerWithIdentityClamp(
            IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction> detectionHelper,
            IAmABoxMigrationCatalog catalog,
            IAmABoxPayloadModeValidator<FakeDbConnection> payloadValidator,
            IAmARelationalDatabaseConfiguration configuration,
            IAmABoxMigrationRunner migrationRunner,
            BoxType boxType)
            : base(detectionHelper, catalog, payloadValidator, configuration, migrationRunner, boxType)
        {
        }

        protected override FakeDbConnection CreateConnection(string connectionString)
            => new FakeDbConnection();

        protected override string PayloadColumnName => "Body";

        protected override int ClampDetectedVersion(int detectedVersion) => detectedVersion;
    }

    /// <summary>
    /// Drives the provisioner into the bootstrap branch (table exists, no history) so
    /// <c>DetectCurrentVersionAsync</c> returns <see cref="DetectedVersionResult"/> — the only
    /// path where <c>ClampDetectedVersion</c> is applied.
    /// </summary>
    private sealed class BootstrapDetectionHelper : IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction>
    {
        public int DetectedVersionResult { get; set; }

        public Task<bool> DoesTableExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
            => Task.FromResult(true);

        public Task<bool> DoesHistoryExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
            => Task.FromResult(false);

        public Task<int> GetMaxVersionAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
            => throw new System.NotSupportedException();

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
            => Task.FromResult(DetectedVersionResult);
    }

    private sealed class NullPayloadValidator : IAmABoxPayloadModeValidator<FakeDbConnection>
    {
        public Task ValidateAsync(
            FakeDbConnection connection,
            string tableName,
            string? schemaName,
            string columnName,
            bool binaryMessagePayload,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class VersionCapturingMigrationRunner : IAmABoxMigrationRunner
    {
        public BoxTableState? CapturedTableState { get; private set; }

        public Task MigrateAsync(
            string tableName,
            string? schemaName,
            BoxType boxType,
            IReadOnlyList<IAmABoxMigration> migrations,
            BoxTableState tableState,
            CancellationToken cancellationToken = default)
        {
            CapturedTableState = tableState;
            return Task.CompletedTask;
        }
    }

    private sealed class NullMigrationCatalog : IAmABoxMigrationCatalog
    {
        public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
            => System.Array.Empty<IAmABoxMigration>();
    }

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
