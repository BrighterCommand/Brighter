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
/// On the bootstrap branch, <c>SqlBoxProvisioner.DetectTableStateAsync</c> clamps the
/// "discriminator missing" sentinel (<c>-1</c>) to 0 before handing <see cref="BoxTableState"/>
/// to the migration runner; non-negative versions pass through unchanged.
/// </summary>
public class SqlBoxProvisionerNegativeVersionClampTests
{
    [Fact]
    public async Task When_detect_table_state_sees_negative_detected_version_it_should_clamp_to_zero()
    {
        //Arrange
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = -1 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisioner(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(0, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    [Fact]
    public async Task When_detect_table_state_sees_positive_detected_version_it_should_pass_through_unchanged()
    {
        //Arrange
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = 3 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var provisioner = new TestSqlBoxProvisioner(
            detection, new NullMigrationCatalog(), new NullPayloadValidator(),
            new RecordingConfiguration(), migrationRunner, BoxType.Outbox);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(3, migrationRunner.CapturedTableState!.CurrentVersion);
    }

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

    // Table exists, no history: the bootstrap branch, the only one that clamps.
    private sealed class BootstrapDetectionHelper : IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction>
    {
        public int DetectedVersionResult { get; set; }

        public Task<bool> DoesTableExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
            => Task.FromResult(true);

        public Task<bool> DoesHistoryExistAsync(
            FakeDbConnection connection, string tableName, string? schemaName, string? historySchema,
            CancellationToken cancellationToken = default,
            FakeDbTransaction? transaction = null)
            => Task.FromResult(false);

        public Task<int> GetMaxVersionAsync(
            FakeDbConnection connection, string tableName, string? schemaName, string? historySchema,
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
            BoxTableName tableName,
            SchemaName? schemaName,
            BoxType boxType,
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
