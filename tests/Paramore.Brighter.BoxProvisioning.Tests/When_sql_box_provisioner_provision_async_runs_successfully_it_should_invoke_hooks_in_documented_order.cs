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
/// <c>SqlBoxProvisioner&lt;TConnection, TTransaction&gt;</c> calls its hooks in a fixed order on each
/// table-state branch (normal, bootstrap, fresh), so derived classes have a stable contract to
/// extend (ADR 0058 §B.5). Pre-lock detection and payload validation share one connection.
/// </summary>
public class SqlBoxProvisionerHookOrderTests
{
    [Fact]
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

        //Assert
        Assert.Equal(
            new[]
            {
                "CreateConnection",
                "OpenAsync",
                "DoesTableExistAsync",
                "DoesHistoryExistAsync",
                "GetMaxVersionAsync",
                "ValidateAsync",
                "Dispose",
                "MigrateAsync"
            },
            log);
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.True(migrationRunner.CapturedTableState!.TableExists);
        Assert.True(migrationRunner.CapturedTableState!.HistoryExists);
        Assert.Equal(5, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    [Fact]
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

        //Assert
        Assert.Equal(
            new[]
            {
                "CreateConnection",
                "OpenAsync",
                "DoesTableExistAsync",
                "DoesHistoryExistAsync",
                "DetectCurrentVersionAsync",
                "ValidateAsync",
                "Dispose",
                "MigrateAsync"
            },
            log);
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.True(migrationRunner.CapturedTableState!.TableExists);
        Assert.False(migrationRunner.CapturedTableState!.HistoryExists);
    }

    [Fact]
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

        //Assert
        Assert.Equal(
            new[]
            {
                "CreateConnection",
                "OpenAsync",
                "DoesTableExistAsync",
                "Dispose",
                "MigrateAsync"
            },
            log);
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.False(migrationRunner.CapturedTableState!.TableExists);
        Assert.False(migrationRunner.CapturedTableState!.HistoryExists);
        Assert.Equal(0, migrationRunner.CapturedTableState!.CurrentVersion);
    }

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

    // Records Dispose via Dispose(bool): DbConnection.Dispose(bool) does not call Close().
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
