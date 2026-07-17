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
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// Spec 0029 FR5/AC5/NF2 (ADR 0060 D5): an operator who provisioned under Global (history in public)
// and later flips the SAME deployment to PerSchema must see their existing history rows seeded
// into the new per-schema __BrighterMigrationHistory before the runner re-detects state, so the
// already-applied migrations are NOT re-run. Without the D5 seed, the flip would create an empty
// per-schema history table, re-detect at V_latest from the box-table columns via the bootstrap
// path, and stamp a NEW history row whose Description and AppliedAt differ from the original.
// This test pins both signals: the seeded row's Description must equal the original ("fresh
// install at V7", NOT "bootstrap: detected at V7"), and its AppliedAt must equal the original
// timestamp (proving the row was COPIED, not re-stamped). A second PerSchema run must be a true
// no-op (NOT EXISTS guard) — same row count, same AppliedAt, no duplicates from a re-seed.
// PG folds unquoted identifiers to lowercase, so the configured non-default schema is addressed
// and filtered in its folded form on both sides.
public class PostgreSqlGlobalToPerSchemaFlipTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_flip_{Guid.NewGuid():N}";
    private readonly string _foldedSchema;

    public PostgreSqlGlobalToPerSchemaFlipTests()
    {
        _foldedSchema = _schemaName.ToLowerInvariant();
    }

    [Test]
    public async Task When_postgres_deployment_flips_from_global_to_per_schema_it_should_not_re_run_applied_migrations()
    {
        //Arrange — clean slate; operator pre-creates the (folded) schema (runner does not).
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_foldedSchema);
        await DropAnyExistingTableAsync(_tableName, _foldedSchema);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _foldedSchema);
        await DeletePublicHistoryRowsAsync();

        //Arrange — provision under Global so history is populated in public (today's behaviour).
        //Box table still lands in the configured non-default schema (F1 behaviour, unchanged).
        var globalProvisioner = BuildOutboxProvisioner(MigrationHistoryScope.Global);
        await globalProvisioner.ProvisionAsync();

        // Sanity-check the arranged precondition so a regression in the Global path doesn't
        // masquerade as a D5 seed failure: exactly one tenant row in public, none in the per-schema
        // location (which the runner should not have created under Global scope).
        await Assert.That(await GetHistoryRowCountInSchemaAsync("public")).IsEqualTo(1);
        await Assert.That(await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema)).IsFalse();
        var (legacyDescription, legacyAppliedAt) = await GetSingleHistoryRowAsync("public");
        await Assert.That(legacyDescription).IsEqualTo($"fresh install at V{ExpectedMigrationVersions.OutboxLatest}");

        //Act — flip the SAME deployment to PerSchema (same SchemaName, same box table) and
        //provision again. The D5 seed must run inside EnsureHistoryTableAsync (under the existing
        //advisory lock + transaction) BEFORE state re-detection, so the per-schema history is
        //pre-populated and the box's already-applied version is recognised.
        var perSchemaProvisioner = BuildOutboxProvisioner(MigrationHistoryScope.PerSchema);
        var flipException = await TestExceptionRecorder.CaptureAsync(() => perSchemaProvisioner.ProvisionAsync());

        //Assert — flip provision succeeds and per-schema history table now exists.
        await Assert.That(flipException).IsNull();
        await Assert.That(await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema)).IsTrue().Because($"Per-schema history table must be created in '{_foldedSchema}' under PerSchema scope.");

        //Assert — per-schema history contains exactly this tenant's prior row (D5 seeded one row,
        //filtered to this tenant's SchemaName + BoxTableName, with NO migration re-applied).
        await Assert.That(await GetHistoryRowCountInSchemaAsync(_foldedSchema)).IsEqualTo(1);

        //Assert — the seeded row preserves the ORIGINAL Description and AppliedAt. If the runner
        //had let the bootstrap path execute against an empty per-schema history, Description would
        //read "bootstrap: detected at V7" and AppliedAt would be a fresh timestamp — both signals
        //that a migration was effectively re-applied. Equality with the legacy row proves the seed
        //copied, didn't re-stamp.
        var (perSchemaDescription, perSchemaAppliedAt) = await GetSingleHistoryRowAsync(_foldedSchema);
        await Assert.That(perSchemaDescription).IsEqualTo(legacyDescription);
        await Assert.That(perSchemaAppliedAt).IsEqualTo(legacyAppliedAt);

        //Assert — original legacy public row is left in place (flip does not delete legacy history;
        //the seed is INSERT-only into the per-schema target).
        await Assert.That(await GetHistoryRowCountInSchemaAsync("public")).IsEqualTo(1);

        //Act — second PerSchema provisioning run must be a true no-op (NOT EXISTS guard in the
        //seed + detection short-circuit on the now-populated per-schema history).
        var secondException = await TestExceptionRecorder.CaptureAsync(() => perSchemaProvisioner.ProvisionAsync());

        //Assert — idempotent: still exactly one per-schema row, AppliedAt unchanged (proves the
        //seed did not re-fire and the runner did not stamp a fresh row).
        await Assert.That(secondException).IsNull();
        await Assert.That(await GetHistoryRowCountInSchemaAsync(_foldedSchema)).IsEqualTo(1);
        var (_, perSchemaAppliedAtAfterSecondRun) = await GetSingleHistoryRowAsync(_foldedSchema);
        await Assert.That(perSchemaAppliedAtAfterSecondRun).IsEqualTo(perSchemaAppliedAt);
    }

    private PostgreSqlOutboxProvisioner BuildOutboxProvisioner(MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: scope);
        return new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);
    }

    private async Task EnsureSchemaExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{schemaName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropSchemaIfExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP SCHEMA IF EXISTS ""{schemaName}"" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropAnyExistingTableAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP TABLE IF EXISTS ""{schemaName}"".""{tableName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> TableExistsInSchemaAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    // Counts this tenant's history rows in the named physical schema's history table. Rows are
    // stamped with the box's configured (folded) SchemaName regardless of where the table
    // physically lives, so filter on that. The history table may be absent in a schema on a clean
    // database; treat "absent" as zero rows so the per-schema absence assertion works under Global
    // scope. PostgreSQL plans the whole statement up front and raises 42P01 if the COUNT references
    // a missing relation — even guarded by CASE/EXISTS — so probe existence in a separate
    // round-trip and short-circuit before touching the table.
    private async Task<long> GetHistoryRowCountInSchemaAsync(string physicalSchema)
    {
        if (!await TableExistsInSchemaAsync("__BrighterMigrationHistory", physicalSchema))
            return 0;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{physicalSchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    // Reads this tenant's single history row's Description + AppliedAt from the named physical
    // schema. Used to compare legacy public row vs seeded per-schema row — equality on both fields
    // is the proof that the seed COPIED rather than re-stamping via the bootstrap path. AppliedAt
    // is returned as the raw scalar (TIMESTAMPTZ) so the comparison is agnostic about Npgsql's CLR
    // mapping; both reads return the same type, so Assert.Equal compares cleanly.
    private async Task<(string Description, object AppliedAt)> GetSingleHistoryRowAsync(string physicalSchema)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT ""Description"", ""AppliedAt"" FROM ""{physicalSchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        await using var reader = await command.ExecuteReaderAsync();
        await Assert.That(await reader.ReadAsync()).IsTrue().Because($"Expected exactly one history row in '{physicalSchema}' for this tenant.");
        var description = reader.GetString(0);
        var appliedAt = reader.GetValue(1);
        await Assert.That(await reader.ReadAsync()).IsFalse().Because($"Expected exactly one history row in '{physicalSchema}' for this tenant, found more.");
        return (description, appliedAt);
    }

    // Removes any history rows this tenant left in public.__BrighterMigrationHistory. Tolerates an
    // absent table (returns silently) by probing existence in a separate round-trip first —
    // Npgsql's @param → $N rewrite does not penetrate the PL/pgSQL body of a DO block, so a single
    // DO-guarded DELETE with parameters fails to bind. Two parameterised statements avoid that.
    private async Task DeletePublicHistoryRowsAsync()
    {
        if (!await TableExistsInSchemaAsync("__BrighterMigrationHistory", "public"))
            return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"DELETE FROM ""public"".""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        await command.ExecuteNonQueryAsync();
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await DropSchemaIfExistsAsync(_foldedSchema);
            await DropAnyExistingTableAsync(_tableName, "public");
            await DeletePublicHistoryRowsAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
