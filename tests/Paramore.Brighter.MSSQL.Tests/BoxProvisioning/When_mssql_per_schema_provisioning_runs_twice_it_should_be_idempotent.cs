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

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// Spec 0029 FR5/NF2/AC3 (ADR 0060 D5): a second PerSchema provisioning run is idempotent. The
// detection-driven short-circuit (T3) reads the per-schema __BrighterMigrationHistory, sees the
// box already at the latest version, and applies NO migration — so the per-schema history row
// count is unchanged and no duplicate rows appear. Asserting the AppliedAt timestamp is also
// unchanged proves the existing row was not deleted-and-reinserted (a count check alone would
// not distinguish that from a true no-op).
public class MsSqlPerSchemaIdempotencyTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_idem_{Guid.NewGuid():N}";
    private readonly MsSqlOutboxProvisioner _provisioner;

    public MsSqlPerSchemaIdempotencyTests()
    {
        // Evident data: PerSchema scope + a non-null SchemaName is the placement case under test.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        _provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_per_schema_provisioning_runs_twice_it_should_apply_no_migrations_and_not_duplicate_history()
    {
        //Arrange — operator pre-creates the schema; runner does not create schemas itself.
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_tableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);

        //Act — first fresh-install run under PerSchema.
        var firstException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — one history row written to the per-schema table; capture its AppliedAt.
        await Assert.That(firstException).IsNull();
        await Assert.That(GetHistoryRowCount()).IsEqualTo(1);
        var appliedAtAfterFirstRun = GetSingleHistoryAppliedAt();

        //Act — second run re-detects from the per-schema history; should apply nothing.
        var secondException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — idempotent: still exactly one row, and the original row is untouched (same
        //AppliedAt), proving no migration was re-applied and no row was rewritten.
        await Assert.That(secondException).IsNull();
        await Assert.That(GetHistoryRowCount()).IsEqualTo(1);
        await Assert.That(GetSingleHistoryAppliedAt()).IsEqualTo(appliedAtAfterFirstRun);
    }

    private void EnsureSchemaExists(string schemaName) =>
        ExecuteNonQuery($@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    EXEC('CREATE SCHEMA [{schemaName}]')");

    private void DropSchemaIfExists(string schemaName) =>
        ExecuteNonQuery($@"
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    DROP SCHEMA [{schemaName}]");

    private void DropAnyExistingTable(string tableName, string schemaName) =>
        ExecuteNonQuery($"DROP TABLE IF EXISTS [{schemaName}].[{tableName}]");

    private int GetHistoryRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT COUNT(1) FROM [{_schemaName}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        return (int)command.ExecuteScalar()!;
    }

    private DateTime GetSingleHistoryAppliedAt()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT [AppliedAt] FROM [{_schemaName}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        return (DateTime)command.ExecuteScalar()!;
    }

    private void ExecuteNonQuery(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public Task DisposeAsync()
    {
        try
        {
            DropAnyExistingTable(_tableName, _schemaName);
            DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
            DropAnyExistingTable(_tableName, "dbo");
            DropSchemaIfExists(_schemaName);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
