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
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// <c>SqlBoxProvisioner.EffectiveSchemaName</c> is a virtual hook so SQLite, which has no schema
/// (ADR 0057 §6), can override it to <c>null</c> (ADR 0058 §B.5). Detection and payload validation
/// use it on both detection branches; the runner's <c>MigrateAsync</c> always gets the configured
/// schema.
/// </summary>
public class SqlBoxProvisionerEffectiveSchemaNameTests
{
    [Fact]
    public async Task When_effective_schema_name_is_default_it_should_pass_configured_schema_to_detection_and_validator_and_runner()
    {
        //Arrange
        var detection = new SchemaCapturingDetectionHelper { TableExistsResult = true };
        var payloadValidator = new SchemaCapturingPayloadValidator();
        var migrationRunner = new SchemaCapturingMigrationRunner();
        var configuration = new RecordingConfiguration();
        var provisioner = new TestSqlBoxProvisionerWithDefaultSchema(
            detection, new RecordingMigrationCatalog(), payloadValidator,
            configuration, migrationRunner, BoxType.Outbox);

        //Act
        await ProvisionOnBootstrapThenHistoryBranchAsync(provisioner, detection);

        //Assert
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
        //Arrange
        var detection = new SchemaCapturingDetectionHelper { TableExistsResult = true };
        var payloadValidator = new SchemaCapturingPayloadValidator();
        var migrationRunner = new SchemaCapturingMigrationRunner();
        var configuration = new RecordingConfiguration();
        var provisioner = new TestSqlBoxProvisionerWithNullSchema(
            detection, new RecordingMigrationCatalog(), payloadValidator,
            configuration, migrationRunner, BoxType.Outbox);

        //Act
        await ProvisionOnBootstrapThenHistoryBranchAsync(provisioner, detection);

        //Assert
        Assert.Equal(new string?[] { null, null }, detection.DoesTableExistSchemas);
        Assert.Equal(new string?[] { null, null }, detection.DoesHistoryExistSchemas);
        Assert.Equal(new string?[] { null }, detection.DetectCurrentVersionSchemas);
        Assert.Equal(new string?[] { null }, detection.GetMaxVersionSchemas);
        Assert.Equal(new string?[] { null, null }, payloadValidator.ValidateSchemas);
        Assert.Equal(new string?[] { "dbo", "dbo" }, migrationRunner.MigrateSchemas);
    }

    private static async Task ProvisionOnBootstrapThenHistoryBranchAsync(
        SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction> provisioner,
        SchemaCapturingDetectionHelper detection)
    {
        detection.HistoryExistsResult = false;
        await provisioner.ProvisionAsync();
        detection.HistoryExistsResult = true;
        await provisioner.ProvisionAsync();
    }

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
            FakeDbConnection connection, string tableName, string? schemaName, string? historySchema,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
        {
            DoesHistoryExistSchemas.Add(schemaName);
            return Task.FromResult(HistoryExistsResult);
        }

        public Task<int> GetMaxVersionAsync(
            FakeDbConnection connection, string tableName, string? schemaName, string? historySchema,
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

    private sealed class SchemaCapturingMigrationRunner : IAmABoxMigrationRunner
    {
        public List<string?> MigrateSchemas { get; } = new();

        public Task MigrateAsync(
            BoxTableName tableName,
            SchemaName? schemaName,
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
        public TimeSpan? OutstandingCountTimeout => null;
    }
}
