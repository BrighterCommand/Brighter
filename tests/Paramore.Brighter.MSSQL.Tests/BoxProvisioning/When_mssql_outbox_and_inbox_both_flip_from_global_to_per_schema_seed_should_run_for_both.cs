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

// Spec 0029 FR5/AC5/NF2 (ADR 0060 D5) — second box-type flip regression.
// Outbox and Inbox share __BrighterMigrationHistory: they are provisioned independently but write
// into the same physical history table differentiated by the BoxTableName column. The first flip
// (e.g. outbox) creates the per-schema __BrighterMigrationHistory table and seeds the outbox row
// from the legacy default-schema history. The SECOND flip (e.g. inbox) MUST also seed its row.
//
// PR #4155 reviewer-found bug: the table-level pre-create existence probe gated the seed on
// "per-schema history table did NOT exist before this run". For the second box-type the table now
// exists (the first flip created it), so the seed was skipped — the inbox's legacy row never
// landed in the per-schema target, and the runner's bootstrap path then stamped a fresh row with
// Description "bootstrap: detected at V_latest" and a new AppliedAt. The fix drops the table-level
// gate and relies on the per-row NOT EXISTS PK guard inside the seed INSERT for idempotency: a
// fresh PerSchema install with no legacy table is still a no-op (the SELECT FROM legacy is gated
// by a legacy-existence probe upstream), and a re-run on already-seeded data finds the composite
// PK already present and inserts zero rows.
//
// Test pins the bug + the fix: BOTH the outbox row AND the inbox row must arrive in the
// per-schema history with their ORIGINAL Description (no "bootstrap:" rewrite) and ORIGINAL
// AppliedAt (no fresh-timestamp restamp). A second run of both provisioners must be a true no-op.
public class MsSqlMultiBoxGlobalToPerSchemaFlipTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _outboxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _inboxTableName = $"test_inbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_multibox_flip_{Guid.NewGuid():N}";

    [Test]
    public async Task When_mssql_outbox_and_inbox_both_flip_from_global_to_per_schema_it_should_seed_both_rows()
    {
        //Arrange — clean slate; operator pre-creates the schema (runner does not create schemas).
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_outboxTableName, _schemaName);
        DropAnyExistingTable(_inboxTableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
        DeleteDboHistoryRows();

        //Arrange — provision BOTH boxes under Global. Both rows land in dbo.__BrighterMigrationHistory
        //differentiated by the BoxTableName column. Box tables themselves still land in the configured
        //schema under Global (F1 behaviour unchanged).
        await BuildOutboxProvisioner(MigrationHistoryScope.Global).ProvisionAsync();
        await BuildInboxProvisioner(MigrationHistoryScope.Global).ProvisionAsync();

        // Sanity-check arranged precondition — exactly TWO tenant rows in dbo (one per box), no
        // per-schema history table yet (Global scope must not create it).
        await Assert.That(GetHistoryRowCountInSchema("dbo")).IsEqualTo(2);
        await Assert.That(TableExistsInSchema("__BrighterMigrationHistory", _schemaName)).IsFalse();
        var (legacyOutboxDescription, legacyOutboxAppliedAt) = await GetSingleHistoryRow("dbo", _outboxTableName);
        var (legacyInboxDescription, legacyInboxAppliedAt) = await GetSingleHistoryRow("dbo", _inboxTableName);
        await Assert.That(legacyOutboxDescription).IsEqualTo($"fresh install at V{ExpectedMigrationVersions.OutboxLatest}");
        await Assert.That(legacyInboxDescription).IsEqualTo($"fresh install at V{ExpectedMigrationVersions.InboxLatest}");

        //Act — flip OUTBOX to PerSchema first. This creates the per-schema __BrighterMigrationHistory
        //table and seeds the outbox row.
        var outboxFlipException = await TestExceptionRecorder.CaptureAsync(
            () => BuildOutboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());
        await Assert.That(outboxFlipException).IsNull();
        await Assert.That(TableExistsInSchema("__BrighterMigrationHistory", _schemaName)).IsTrue().Because($"Per-schema history table must be created in '{_schemaName}' by the first flip.");
        await Assert.That(GetHistoryRowCountInSchema(_schemaName)).IsEqualTo(1);

        //Act — flip INBOX to PerSchema. The per-schema history table now exists. With the BUG
        //(pre-fix), the table-level gate `perSchemaExisted=true` skips the seed; the inbox row
        //is never copied; the bootstrap path stamps a fresh "bootstrap: detected at V..." row.
        //With the FIX (no table-level gate, per-row NOT EXISTS PK guard only), the inbox row is
        //seeded from dbo and the original Description + AppliedAt are preserved.
        var inboxFlipException = await TestExceptionRecorder.CaptureAsync(
            () => BuildInboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());
        await Assert.That(inboxFlipException).IsNull();

        //Assert — per-schema history now contains BOTH rows.
        await Assert.That(GetHistoryRowCountInSchema(_schemaName)).IsEqualTo(2);

        //Assert — BOTH rows preserve original Description AND AppliedAt (the bug surfaces as the
        //inbox row carrying "bootstrap: detected at V..." with a fresh timestamp; either
        //assertion alone would catch the regression, both together pin both signals).
        var (perSchemaOutboxDescription, perSchemaOutboxAppliedAt) =
            await GetSingleHistoryRow(_schemaName, _outboxTableName);
        await Assert.That(perSchemaOutboxDescription).IsEqualTo(legacyOutboxDescription);
        await Assert.That(perSchemaOutboxAppliedAt).IsEqualTo(legacyOutboxAppliedAt);

        var (perSchemaInboxDescription, perSchemaInboxAppliedAt) =
            await GetSingleHistoryRow(_schemaName, _inboxTableName);
        await Assert.That(perSchemaInboxDescription).IsEqualTo(legacyInboxDescription);
        await Assert.That(perSchemaInboxAppliedAt).IsEqualTo(legacyInboxAppliedAt);

        //Act — re-run both PerSchema provisioners. Idempotency (NOT EXISTS PK guard + runner
        //short-circuit on already-applied state) must hold for repeated flips.
        var rerunOutboxException = await TestExceptionRecorder.CaptureAsync(
            () => BuildOutboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());
        var rerunInboxException = await TestExceptionRecorder.CaptureAsync(
            () => BuildInboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());

        //Assert — still exactly two rows; AppliedAts unchanged on both rows.
        await Assert.That(rerunOutboxException).IsNull();
        await Assert.That(rerunInboxException).IsNull();
        await Assert.That(GetHistoryRowCountInSchema(_schemaName)).IsEqualTo(2);
        var (_, perSchemaOutboxAppliedAtAfterRerun) = await GetSingleHistoryRow(_schemaName, _outboxTableName);
        var (_, perSchemaInboxAppliedAtAfterRerun) = await GetSingleHistoryRow(_schemaName, _inboxTableName);
        await Assert.That(perSchemaOutboxAppliedAtAfterRerun).IsEqualTo(perSchemaOutboxAppliedAt);
        await Assert.That(perSchemaInboxAppliedAtAfterRerun).IsEqualTo(perSchemaInboxAppliedAt);
    }

    private MsSqlOutboxProvisioner BuildOutboxProvisioner(MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _outboxTableName,
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

    private MsSqlInboxProvisioner BuildInboxProvisioner(MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _inboxTableName,
            schemaName: _schemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: scope);
        return new MsSqlInboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlInboxMigrationCatalog(),
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

    // Counts this tenant's history rows (filtered by SchemaName — both box rows share it) in the
    // named physical schema's history table. Tolerates an absent table (returns 0).
    private int GetHistoryRowCountInSchema(string physicalSchema)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"IF OBJECT_ID('[{physicalSchema}].[__BrighterMigrationHistory]', 'U') IS NULL " +
            "SELECT 0; " +
            $"ELSE SELECT COUNT(1) FROM [{physicalSchema}].[__BrighterMigrationHistory] " +
            "WHERE [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        return (int)command.ExecuteScalar()!;
    }

    // Reads one history row's Description + AppliedAt for a given (BoxTableName, SchemaName).
    // Equality of the per-schema row against the legacy row is what proves the seed copied rather
    // than re-stamped via the bootstrap path.
    private async Task<(string Description, DateTime AppliedAt)> GetSingleHistoryRow(string physicalSchema, string boxTableName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT [Description], [AppliedAt] FROM [{physicalSchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", boxTableName);
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        using var reader = command.ExecuteReader();
        await Assert.That(reader.Read()).IsTrue().Because($"Expected exactly one history row in [{physicalSchema}] for ({boxTableName}, {_schemaName}).");
        var description = reader.GetString(0);
        var appliedAt = reader.GetDateTime(1);
        await Assert.That(reader.Read()).IsFalse().Because($"Expected exactly one history row in [{physicalSchema}] for ({boxTableName}, {_schemaName}); found more.");
        return (description, appliedAt);
    }

    private void DeleteDboHistoryRows() =>
        ExecuteNonQuery(
            "IF OBJECT_ID('[dbo].[__BrighterMigrationHistory]', 'U') IS NOT NULL " +
            $"DELETE FROM [dbo].[__BrighterMigrationHistory] " +
            $"WHERE [BoxTableName] IN ('{_outboxTableName}', '{_inboxTableName}')");

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
            DropAnyExistingTable(_outboxTableName, _schemaName);
            DropAnyExistingTable(_inboxTableName, _schemaName);
            DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
            DropAnyExistingTable(_outboxTableName, "dbo");
            DropAnyExistingTable(_inboxTableName, "dbo");
            DeleteDboHistoryRows();
            DropSchemaIfExists(_schemaName);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
