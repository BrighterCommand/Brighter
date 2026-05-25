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
/// The <c>SqlBoxProvisioner</c> base exposes <c>EffectiveSchemaName</c> as a virtual hook so
/// SQLite (which has no schema concept per ADR 0057 §6) can override to <c>null</c> without
/// affecting the runner call (per ADR 0058 §B.5 line 608-614). The detection-helper and
/// payload-validator calls inside <c>DetectTableStateAsync</c> and <c>ValidatePayloadModeAsync</c>
/// observe this property, while the runner's <c>MigrateAsync</c> call inside <c>ProvisionAsync</c>
/// always propagates <c>_configuration.SchemaName</c> directly. These tests pin the propagation
/// contract on both code paths (default and override-to-null) across both detection branches
/// (bootstrap and history-exists) so all five detection/validator call sites are exercised.
/// </summary>
public class SqlBoxProvisionerEffectiveSchemaNameTests
{
    [Fact]
    public async Task When_effective_schema_name_is_default_it_should_pass_configured_schema_to_detection_and_validator_and_runner()
    {
        //Arrange — derivation with NO override: inherits the default `_configuration.SchemaName`.
        var detection = new SchemaCapturingDetectionHelper { TableExistsResult = true };
        var payloadValidator = new SchemaCapturingPayloadValidator();
        var migrationRunner = new SchemaCapturingMigrationRunner();
        var configuration = new RecordingConfiguration(); // SchemaName = "dbo"
        var provisioner = new TestSqlBoxProvisionerWithDefaultSchema(
            detection, new RecordingMigrationCatalog(), payloadValidator,
            configuration, migrationRunner, BoxType.Outbox);

        //Act — run twice to exercise both detection branches:
        //  pass 1: bootstrap branch (DoesTableExist + DoesHistoryExist + DetectCurrentVersion + Validate + Migrate)
        detection.HistoryExistsResult = false;
        await provisioner.ProvisionAsync();
        //  pass 2: history-exists branch (DoesTableExist + DoesHistoryExist + GetMaxVersion + Validate + Migrate)
        detection.HistoryExistsResult = true;
        await provisioner.ProvisionAsync();

        //Assert — every detection/validator call observes the configured "dbo"; both runner calls
        //also observe "dbo" (the runner call propagates `_configuration.SchemaName` directly).
        Assert.Equal(new string?[] { "dbo", "dbo" }, detection.DoesTableExistSchemas);
        Assert.Equal(new string?[] { "dbo", "dbo" }, detection.DoesHistoryExistSchemas);
        Assert.Equal(new string?[] { "dbo" }, detection.DetectCurrentVersionSchemas);
        Assert.Equal(new string?[] { "dbo" }, detection.GetMaxVersionSchemas);
        Assert.Equal(new string?[] { "dbo", "dbo" }, payloadValidator.ValidateSchemas);
        Assert.Equal(new string?[] { "dbo", "dbo" }, migrationRunner.MigrateSchemas);
    }

    [Fact]
    public async Task When_effective_schema_name_is_overridden_to_null_it_should_pass_null_to_detection_and_validator_but_configured_schema_to_runner()
    {
        //Arrange — derivation overrides `EffectiveSchemaName` to null (the SQLite shape).
        var detection = new SchemaCapturingDetectionHelper { TableExistsResult = true };
        var payloadValidator = new SchemaCapturingPayloadValidator();
        var migrationRunner = new SchemaCapturingMigrationRunner();
        var configuration = new RecordingConfiguration(); // SchemaName = "dbo"
        var provisioner = new TestSqlBoxProvisionerWithNullSchema(
            detection, new RecordingMigrationCatalog(), payloadValidator,
            configuration, migrationRunner, BoxType.Outbox);

        //Act — both branches.
        detection.HistoryExistsResult = false;
        await provisioner.ProvisionAsync();
        detection.HistoryExistsResult = true;
        await provisioner.ProvisionAsync();

        //Assert — every detection/validator call observes null (the override); the runner call
        //still observes the configured "dbo" — only detection/validation routes through
        //EffectiveSchemaName, per ADR §B.5 line 611-612.
        Assert.Equal(new string?[] { null, null }, detection.DoesTableExistSchemas);
        Assert.Equal(new string?[] { null, null }, detection.DoesHistoryExistSchemas);
        Assert.Equal(new string?[] { null }, detection.DetectCurrentVersionSchemas);
        Assert.Equal(new string?[] { null }, detection.GetMaxVersionSchemas);
        Assert.Equal(new string?[] { null, null }, payloadValidator.ValidateSchemas);
        Assert.Equal(new string?[] { "dbo", "dbo" }, migrationRunner.MigrateSchemas);
    }

    /// <summary>
    /// Derivation with no <c>EffectiveSchemaName</c> override — inherits the base default
    /// (<c>_configuration.SchemaName</c>). Represents the MSSQL/PostgreSQL/MySQL shape.
    /// </summary>
    private sealed class TestSqlBoxProvisionerWithDefaultSchema : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public TestSqlBoxProvisionerWithDefaultSchema(
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
    /// Derivation that overrides <c>EffectiveSchemaName</c> to null — represents the SQLite shape
    /// (no schema concept per ADR 0057 §6).
    /// </summary>
    private sealed class TestSqlBoxProvisionerWithNullSchema : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public TestSqlBoxProvisionerWithNullSchema(
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

        protected override string? EffectiveSchemaName => null;
    }

    /// <summary>
    /// Captures the <c>schemaName</c> argument passed on every detection-helper call so the
    /// assertion can verify per-call propagation across both detection branches.
    /// </summary>
    private sealed class SchemaCapturingDetectionHelper : IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction>
    {
        public bool TableExistsResult { get; set; }
        public bool HistoryExistsResult { get; set; }
        public int MaxVersionResult { get; set; }
        public int DetectedVersionResult { get; set; }

        public List<string?> DoesTableExistSchemas { get; } = new();
        public List<string?> DoesHistoryExistSchemas { get; } = new();
        public List<string?> GetMaxVersionSchemas { get; } = new();
        public List<string?> DetectCurrentVersionSchemas { get; } = new();

        public Task<bool> DoesTableExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            DoesTableExistSchemas.Add(schemaName);
            return Task.FromResult(TableExistsResult);
        }

        public Task<bool> DoesHistoryExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            DoesHistoryExistSchemas.Add(schemaName);
            return Task.FromResult(HistoryExistsResult);
        }

        public Task<int> GetMaxVersionAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            GetMaxVersionSchemas.Add(schemaName);
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
            DetectCurrentVersionSchemas.Add(schemaName);
            return Task.FromResult(DetectedVersionResult);
        }
    }

    /// <summary>
    /// Captures the <c>schemaName</c> argument passed on every payload-validator call.
    /// </summary>
    private sealed class SchemaCapturingPayloadValidator : IAmABoxPayloadModeValidator<FakeDbConnection>
    {
        public List<string?> ValidateSchemas { get; } = new();

        public Task ValidateAsync(
            FakeDbConnection connection,
            string tableName,
            string? schemaName,
            string columnName,
            bool binaryMessagePayload,
            CancellationToken cancellationToken = default)
        {
            ValidateSchemas.Add(schemaName);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Captures the <c>schemaName</c> argument passed to <c>MigrateAsync</c> so the assertion
    /// can verify the runner-call schema is always the configured value (never the override).
    /// </summary>
    private sealed class SchemaCapturingMigrationRunner : IAmABoxMigrationRunner
    {
        public List<string?> MigrateSchemas { get; } = new();

        public Task MigrateAsync(
            string tableName,
            string? schemaName,
            BoxType boxType,
            BoxTableState tableState,
            CancellationToken cancellationToken = default)
        {
            MigrateSchemas.Add(schemaName);
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
