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
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// Spec 0029 FR5/AC5/NF2 (ADR 0060 D5) — second box-type flip regression (PG mirror of the MSSQL
// counterpart). See MsSqlMultiBoxGlobalToPerSchemaFlipTests for the canonical narrative; this is
// the PG-side characterization. PG folds unquoted identifiers to lowercase, so schemas are
// addressed and filtered in folded form on both sides.
//
// PR #4155 reviewer-found bug: the table-level pre-create existence probe gated the seed on "per-
// schema history table did NOT exist before this run". For the second box-type the table now
// exists (the first flip created it), so the seed was skipped — the inbox's legacy row never
// landed in the per-schema target, and the runner's bootstrap path then stamped a fresh row with
// Description "bootstrap: detected at V_latest" and a new AppliedAt. The fix drops the table-level
// gate and relies on the per-row NOT EXISTS PK guard inside the seed INSERT for idempotency.
public class PostgreSqlMultiBoxGlobalToPerSchemaFlipTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _outboxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _inboxTableName = $"test_inbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_multibox_flip_{Guid.NewGuid():N}";
    private readonly string _foldedSchema;
    private readonly string _foldedOutbox;
    private readonly string _foldedInbox;

    public PostgreSqlMultiBoxGlobalToPerSchemaFlipTests()
    {
        _foldedSchema = _schemaName.ToLowerInvariant();
        _foldedOutbox = _outboxTableName.ToLowerInvariant();
        _foldedInbox = _inboxTableName.ToLowerInvariant();
    }

    [Fact]
    public async Task When_postgres_outbox_and_inbox_both_flip_from_global_to_per_schema_it_should_seed_both_rows()
    {
        //Arrange — clean slate; operator pre-creates the (folded) schema (runner does not).
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_foldedSchema);
        await DropAnyExistingTableAsync(_foldedOutbox, _foldedSchema);
        await DropAnyExistingTableAsync(_foldedInbox, _foldedSchema);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _foldedSchema);
        await DeletePublicHistoryRowsAsync();

        //Arrange — provision BOTH boxes under Global. Both rows land in public.__BrighterMigrationHistory
        //differentiated by the BoxTableName column. Box tables themselves still land in the configured
        //(folded) schema under Global (F1 behaviour unchanged).
        await BuildOutboxProvisioner(MigrationHistoryScope.Global).ProvisionAsync();
        await BuildInboxProvisioner(MigrationHistoryScope.Global).ProvisionAsync();

        // Sanity-check arranged precondition — exactly TWO tenant rows in public, no per-schema
        // history table yet (Global scope must not create it).
        Assert.Equal(2, await GetHistoryRowCountInSchemaAsync("public"));
        Assert.False(await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema));
        var (legacyOutboxDescription, legacyOutboxAppliedAt) = await GetSingleHistoryRowAsync("public", _foldedOutbox);
        var (legacyInboxDescription, legacyInboxAppliedAt) = await GetSingleHistoryRowAsync("public", _foldedInbox);
        Assert.Equal($"fresh install at V{ExpectedMigrationVersions.OutboxLatest}", legacyOutboxDescription);
        Assert.Equal($"fresh install at V{ExpectedMigrationVersions.InboxLatest}", legacyInboxDescription);

        //Act — flip OUTBOX to PerSchema first. This creates the per-schema __BrighterMigrationHistory
        //table and seeds the outbox row.
        var outboxFlipException = await Record.ExceptionAsync(
            () => BuildOutboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());
        Assert.Null(outboxFlipException);
        Assert.True(
            await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema),
            $"Per-schema history table must be created in '{_foldedSchema}' by the first flip.");
        Assert.Equal(1, await GetHistoryRowCountInSchemaAsync(_foldedSchema));

        //Act — flip INBOX to PerSchema. The per-schema history table now exists. With the BUG
        //(pre-fix), the table-level gate `perSchemaExisted=true` skips the seed; the inbox row
        //is never copied; the bootstrap path stamps a fresh "bootstrap: detected at V..." row.
        //With the FIX (no table-level gate, per-row NOT EXISTS PK guard only), the inbox row is
        //seeded from public and the original Description + AppliedAt are preserved.
        var inboxFlipException = await Record.ExceptionAsync(
            () => BuildInboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());
        Assert.Null(inboxFlipException);

        //Assert — per-schema history now contains BOTH rows.
        Assert.Equal(2, await GetHistoryRowCountInSchemaAsync(_foldedSchema));

        //Assert — BOTH rows preserve original Description AND AppliedAt (the bug surfaces as the
        //inbox row carrying "bootstrap: detected at V..." with a fresh timestamp; either assertion
        //alone would catch the regression, both together pin both signals).
        var (perSchemaOutboxDescription, perSchemaOutboxAppliedAt) =
            await GetSingleHistoryRowAsync(_foldedSchema, _foldedOutbox);
        Assert.Equal(legacyOutboxDescription, perSchemaOutboxDescription);
        Assert.Equal(legacyOutboxAppliedAt, perSchemaOutboxAppliedAt);

        var (perSchemaInboxDescription, perSchemaInboxAppliedAt) =
            await GetSingleHistoryRowAsync(_foldedSchema, _foldedInbox);
        Assert.Equal(legacyInboxDescription, perSchemaInboxDescription);
        Assert.Equal(legacyInboxAppliedAt, perSchemaInboxAppliedAt);

        //Act — re-run both PerSchema provisioners. Idempotency (NOT EXISTS PK guard + runner
        //short-circuit on already-applied state) must hold for repeated flips.
        var rerunOutboxException = await Record.ExceptionAsync(
            () => BuildOutboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());
        var rerunInboxException = await Record.ExceptionAsync(
            () => BuildInboxProvisioner(MigrationHistoryScope.PerSchema).ProvisionAsync());

        //Assert — still exactly two rows; AppliedAts unchanged on both rows.
        Assert.Null(rerunOutboxException);
        Assert.Null(rerunInboxException);
        Assert.Equal(2, await GetHistoryRowCountInSchemaAsync(_foldedSchema));
        var (_, perSchemaOutboxAppliedAtAfterRerun) = await GetSingleHistoryRowAsync(_foldedSchema, _foldedOutbox);
        var (_, perSchemaInboxAppliedAtAfterRerun) = await GetSingleHistoryRowAsync(_foldedSchema, _foldedInbox);
        Assert.Equal(perSchemaOutboxAppliedAt, perSchemaOutboxAppliedAtAfterRerun);
        Assert.Equal(perSchemaInboxAppliedAt, perSchemaInboxAppliedAtAfterRerun);
    }

    private PostgreSqlOutboxProvisioner BuildOutboxProvisioner(MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _outboxTableName,
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

    private PostgreSqlInboxProvisioner BuildInboxProvisioner(MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _inboxTableName,
            schemaName: _schemaName);
        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: scope);
        return new PostgreSqlInboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlInboxMigrationCatalog(),
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

    // Counts this tenant's history rows (filtered by SchemaName — both box rows share it) in the
    // named physical schema's history table. Tolerates an absent table by probing existence in a
    // separate round-trip first — PG plans the whole statement and raises 42P01 on a missing
    // relation even guarded by CASE/EXISTS (T6 lesson).
    private async Task<long> GetHistoryRowCountInSchemaAsync(string physicalSchema)
    {
        if (!await TableExistsInSchemaAsync("__BrighterMigrationHistory", physicalSchema))
            return 0;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{physicalSchema}"".""__BrighterMigrationHistory""
WHERE ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    // Reads one history row's Description + AppliedAt for a given (BoxTableName, SchemaName).
    // Equality of the per-schema row against the legacy row is what proves the seed copied rather
    // than re-stamped via the bootstrap path. AppliedAt returned as the raw scalar (TIMESTAMPTZ)
    // so the cross-row comparison is agnostic about Npgsql's CLR mapping (both reads return the
    // same provider type).
    private async Task<(string Description, object AppliedAt)> GetSingleHistoryRowAsync(string physicalSchema, string boxTableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT ""Description"", ""AppliedAt"" FROM ""{physicalSchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", boxTableName);
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(),
            $"Expected exactly one history row in '{physicalSchema}' for ({boxTableName}, {_foldedSchema}).");
        var description = reader.GetString(0);
        var appliedAt = reader.GetValue(1);
        Assert.False(await reader.ReadAsync(),
            $"Expected exactly one history row in '{physicalSchema}' for ({boxTableName}, {_foldedSchema}); found more.");
        return (description, appliedAt);
    }

    // Removes any history rows for THIS tenant's outbox+inbox from public.__BrighterMigrationHistory.
    // Probes existence in a separate round-trip first — Npgsql's @param → $N rewrite does not
    // penetrate the PL/pgSQL body of a DO block, so a single DO-guarded DELETE with parameters
    // fails to bind (T9 lesson).
    private async Task DeletePublicHistoryRowsAsync()
    {
        if (!await TableExistsInSchemaAsync("__BrighterMigrationHistory", "public"))
            return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            @"DELETE FROM ""public"".""__BrighterMigrationHistory"" " +
            @"WHERE ""BoxTableName"" IN (@OutboxTableName, @InboxTableName)";
        command.Parameters.AddWithValue("@OutboxTableName", _foldedOutbox);
        command.Parameters.AddWithValue("@InboxTableName", _foldedInbox);
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await DropSchemaIfExistsAsync(_foldedSchema);
            await DropAnyExistingTableAsync(_foldedOutbox, "public");
            await DropAnyExistingTableAsync(_foldedInbox, "public");
            await DeletePublicHistoryRowsAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
