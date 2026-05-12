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
/// <c>SqlBoxProvisioner.DetectTableStateAsync</c> inlines a negative-version clamp on the
/// pre-lock-detected version: spec 0027's <c>-1</c> "discriminator missing" sentinel maps to
/// 0 before being recorded on the <see cref="BoxTableState"/> handed to the migration runner;
/// non-negative values pass through unchanged. These tests pin that behaviour at the call
/// site by data-flow — the captured <c>BoxTableState.CurrentVersion</c> traverses only the
/// bootstrap branch of <c>DetectTableStateAsync</c>, so an accidental relocation or
/// elimination of the inlined clamp would break this test.
/// </summary>
/// <remarks>
/// Pre-13.B this behaviour was hosted in a virtual <c>ClampDetectedVersion</c> hook that
/// MySQL overrode to identity for behavioural neutrality of the structural pull-up. Phase
/// 13.B (F11) unified MySQL's clamp behaviour with the other three relational backends and,
/// in one commit, removed both the MySQL override AND the base hook, inlining the clamp
/// into <c>DetectTableStateAsync</c>. The MySQL integration test in
/// <c>tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_pre_lock_detects_negative_version_it_should_clamp_to_zero.cs</c>
/// pins the same behaviour from the backend side.
/// </remarks>
public class SqlBoxProvisionerNegativeVersionClampTests
{
    [Fact]
    public async Task When_detect_table_state_sees_negative_detected_version_it_should_clamp_to_zero()
    {
        //Arrange — bootstrap branch (table exists, no history); DetectCurrentVersionAsync
        //returns -1 (spec 0027's "discriminator missing" sentinel). The inlined clamp in
        //DetectTableStateAsync maps -1 → 0 before recording on BoxTableState.
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = -1 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisioner(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — captured BoxTableState.CurrentVersion clamped from -1 to 0.
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(0, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    [Fact]
    public async Task When_detect_table_state_sees_positive_detected_version_it_should_pass_through_unchanged()
    {
        //Arrange — bootstrap branch (table exists, no history); DetectCurrentVersionAsync
        //returns 3 (a real V_k). The inlined clamp leaves non-negatives untouched.
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = 3 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisioner(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — captured BoxTableState.CurrentVersion preserves 3.
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(3, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    /// <summary>
    /// Concrete derivation supplying only the abstract hooks. Represents the post-13.B shape:
    /// no derivation overrides clamp behaviour because the hook no longer exists; the inlined
    /// clamp in <c>DetectTableStateAsync</c> applies uniformly across all eight derivations.
    /// </summary>
    private sealed class TestSqlBoxProvisioner : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public TestSqlBoxProvisioner(
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
    /// Drives the provisioner into the bootstrap branch (table exists, no history) so
    /// <c>DetectCurrentVersionAsync</c> returns <see cref="DetectedVersionResult"/> — the only
    /// path where the inlined clamp applies.
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
