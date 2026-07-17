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

// Spec 0029 FR5/AC5/NF2 (ADR 0060 D5): an operator who provisioned under Global (history in dbo)
// and later flips the SAME deployment to PerSchema must see their existing history rows seeded
// into the new per-schema __BrighterMigrationHistory before the runner re-detects state, so the
// already-applied migrations are NOT re-run. Without the D5 seed, the flip would create an empty
// per-schema history table, re-detect at V_latest from the box-table columns via the bootstrap
// path, and stamp a NEW history row whose Description and AppliedAt differ from the original.
// This test pins both signals: the seeded row's Description must equal the original ("fresh
// install at V7", NOT "bootstrap: detected at V7"), and its AppliedAt must equal the original
// timestamp (proving the row was COPIED, not re-stamped). A second PerSchema run must be a true
// no-op (NOT EXISTS guard) — same row count, same AppliedAt, no duplicates from a re-seed.
public class MsSqlGlobalToPerSchemaFlipTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_flip_{Guid.NewGuid():N}";

    [Test]
    public async Task When_mssql_deployment_flips_from_global_to_per_schema_it_should_not_re_run_applied_migrations()
    {
        //Arrange — clean slate; operator pre-creates the schema (runner does not create schemas).
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_tableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
        DeleteDboHistoryRows();

        //Arrange — provision under Global so history is populated in dbo (today's behaviour).
        //Box table still lands in the configured non-default schema (F1 behaviour, unchanged).
        var globalProvisioner = BuildOutboxProvisioner(MigrationHistoryScope.Global);
        await globalProvisioner.ProvisionAsync();

        // Sanity-check the arranged precondition so a regression in the Global path doesn't
        // masquerade as a D5 seed failure: exactly one tenant row in dbo, none in the per-schema
        // location (which the runner should not have created under Global scope).
        await Assert.That(GetHistoryRowCountInSchema("dbo")).IsEqualTo(1);
        await Assert.That(TableExistsInSchema("__BrighterMigrationHistory", _schemaName)).IsFalse();
        var (legacyDescription, legacyAppliedAt) = await GetSingleHistoryRow("dbo");
        await Assert.That(legacyDescription).IsEqualTo($"fresh install at V{ExpectedMigrationVersions.OutboxLatest}");

        //Act — flip the SAME deployment to PerSchema (same SchemaName, same box table) and
        //provision again. The D5 seed must run inside EnsureHistoryTableAsync (under the existing
        //advisory lock + transaction) BEFORE state re-detection, so the per-schema history is
        //pre-populated and the box's already-applied version is recognised.
        var perSchemaProvisioner = BuildOutboxProvisioner(MigrationHistoryScope.PerSchema);
        var flipException = await TestExceptionRecorder.CaptureAsync(() => perSchemaProvisioner.ProvisionAsync());

        //Assert — flip provision succeeds and per-schema history table now exists.
        await Assert.That(flipException).IsNull();
        await Assert.That(TableExistsInSchema("__BrighterMigrationHistory", _schemaName)).IsTrue().Because($"Per-schema history table must be created in '{_schemaName}' under PerSchema scope.");

        //Assert — per-schema history contains exactly this tenant's prior row (D5 seeded one row,
        //filtered to this tenant's SchemaName + BoxTableName, with NO migration re-applied).
        await Assert.That(GetHistoryRowCountInSchema(_schemaName)).IsEqualTo(1);

        //Assert — the seeded row preserves the ORIGINAL Description and AppliedAt. If the runner
        //had let the bootstrap path execute against an empty per-schema history, Description would
        //read "bootstrap: detected at V7" and AppliedAt would be a fresh timestamp — both signals
        //that a migration was effectively re-applied. Equality with the legacy row proves the seed
        //copied, didn't re-stamp.
        var (perSchemaDescription, perSchemaAppliedAt) = await GetSingleHistoryRow(_schemaName);
        await Assert.That(perSchemaDescription).IsEqualTo(legacyDescription);
        await Assert.That(perSchemaAppliedAt).IsEqualTo(legacyAppliedAt);

        //Assert — original legacy dbo row is left in place (flip does not delete legacy history;
        //the seed is INSERT-only into the per-schema target).
        await Assert.That(GetHistoryRowCountInSchema("dbo")).IsEqualTo(1);

        //Act — second PerSchema provisioning run must be a true no-op (NOT EXISTS guard in the
        //seed + detection short-circuit on the now-populated per-schema history).
        var secondException = await TestExceptionRecorder.CaptureAsync(() => perSchemaProvisioner.ProvisionAsync());

        //Assert — idempotent: still exactly one per-schema row, AppliedAt unchanged (proves the
        //seed did not re-fire and the runner did not stamp a fresh row).
        await Assert.That(secondException).IsNull();
        await Assert.That(GetHistoryRowCountInSchema(_schemaName)).IsEqualTo(1);
        var (_, perSchemaAppliedAtAfterSecondRun) = await GetSingleHistoryRow(_schemaName);
        await Assert.That(perSchemaAppliedAtAfterSecondRun).IsEqualTo(perSchemaAppliedAt);
    }

    private MsSqlOutboxProvisioner BuildOutboxProvisioner(MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: scope);
        return new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
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

    private bool TableExistsInSchema(string tableName, string schemaName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM sys.tables t " +
            "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE t.name = @TableName AND s.name = @SchemaName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return (int)command.ExecuteScalar()! > 0;
    }

    // Counts this tenant's history rows in the named physical schema's history table. Tolerates
    // an absent table (returns 0) so the per-schema absence assertion works under Global scope.
    private int GetHistoryRowCountInSchema(string physicalSchema)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"IF OBJECT_ID('[{physicalSchema}].[__BrighterMigrationHistory]', 'U') IS NULL " +
            "SELECT 0; " +
            $"ELSE SELECT COUNT(1) FROM [{physicalSchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        return (int)command.ExecuteScalar()!;
    }

    // Reads this tenant's single history row's Description + AppliedAt from the named physical
    // schema. Used to compare legacy dbo row vs seeded per-schema row — equality on both fields is
    // the proof that the seed COPIED rather than re-stamping via the bootstrap path.
    private async Task<(string Description, DateTime AppliedAt)> GetSingleHistoryRow(string physicalSchema)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT [Description], [AppliedAt] FROM [{physicalSchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        using var reader = command.ExecuteReader();
        await Assert.That(reader.Read()).IsTrue().Because($"Expected exactly one history row in [{physicalSchema}] for this tenant.");
        var description = reader.GetString(0);
        var appliedAt = reader.GetDateTime(1);
        await Assert.That(reader.Read()).IsFalse().Because($"Expected exactly one history row in [{physicalSchema}] for this tenant, found more.");
        return (description, appliedAt);
    }

    private void DeleteDboHistoryRows() =>
        ExecuteNonQuery(
            "IF OBJECT_ID('[dbo].[__BrighterMigrationHistory]', 'U') IS NOT NULL " +
            $"DELETE FROM [dbo].[__BrighterMigrationHistory] WHERE [BoxTableName] = '{_tableName}'");

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
            DeleteDboHistoryRows();
            DropSchemaIfExists(_schemaName);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
