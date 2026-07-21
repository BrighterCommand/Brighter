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

// Spec 0029 FR2/FR3/NF4/AC1 (ADR 0060 D2+D4): on a placement backend (PostgreSQL) with
// MigrationHistoryScope.PerSchema and a non-null SchemaName, the migration-history table is
// physically created in THAT schema (not public). Detection (existence + max version) and writes
// (CREATE + INSERT) all target the same per-schema table, so a second run re-detects from the
// per-schema history and does not re-run the migration.
//
// PostgreSQL folds unquoted identifiers to lowercase, and PgIdentifier.Quote/Normalize both
// lowercase before quoting. A MIXED-CASE SchemaName ("Billing_PerSchema_...") therefore folds to
// the same physical schema on BOTH the write side (CREATE/INSERT) and the read side (existence +
// COUNT + MAX). This test pins that identical fold: the operator pre-creates the FOLDED schema,
// configures the mixed-case value, and the runner must find — on the second run — exactly the
// per-schema history table it created on the first. A regression where one side preserved case
// (e.g. quoted "Billing") and the other lowercased would create in "billing" but read from
// "Billing" (or vice versa), miss the row, and re-run the migration / duplicate history.
public class PostgreSqlOutboxProvisionerSchemaTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    // Evident data: a MIXED-CASE schema name is the regression case under test. PG folds it to
    // lowercase, so the physical schema is the lowercased form below.
    private readonly string _schemaName = $"Billing_PerSchema_{Guid.NewGuid():N}";
    private readonly string _foldedSchema;

    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public PostgreSqlOutboxProvisionerSchemaTests()
    {
        _foldedSchema = _schemaName.ToLowerInvariant();

        // Evident data: PerSchema scope + a non-null SchemaName is the placement case under test.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        _provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_postgres_per_schema_scope_is_selected_it_should_create_history_table_in_configured_schema()
    {
        //Arrange — operator pre-creates the (folded) schema; runner does not create schemas itself.
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_foldedSchema);
        await DropAnyExistingTableAsync(_tableName, _foldedSchema);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _foldedSchema);

        //Act — first fresh-install run under PerSchema
        var firstException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — history table lives in the configured (folded) schema.
        await Assert.That(firstException).IsNull();
        await Assert.That(await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema)).IsTrue().Because($"History table must exist in '{_foldedSchema}' under PerSchema scope.");

        //Assert — the box table and a history row recording its (folded) SchemaName are present per-schema.
        await Assert.That(await TableExistsInSchemaAsync(_tableName, _foldedSchema)).IsTrue();
        await Assert.That(await GetHistoryRowCountAsync(_foldedSchema, _tableName)).IsEqualTo(1);

        //Assert — no fall-back to public: this tenant wrote no row to the shared public history table.
        // (public.__BrighterMigrationHistory is a global table that may pre-exist from Global-scope
        // tests, so the meaningful check is row-level, not table existence.)
        await Assert.That(await GetHistoryRowCountAsync("public", _tableName)).IsEqualTo(0);

        //Act — second run re-detects from the per-schema history; identical folding finds the same
        // table, so no migration re-run.
        var secondException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — idempotent: still exactly one history row in the per-schema table.
        await Assert.That(secondException).IsNull();
        await Assert.That(await GetHistoryRowCountAsync(_foldedSchema, _tableName)).IsEqualTo(1);
    }

    private async Task EnsureSchemaExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{schemaName}""";
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

    private async Task<long> GetHistoryRowCountAsync(string schemaName, string tableName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // The history table may be absent in this schema on a clean database; treat "absent" as
        // zero rows so the public no-fall-back check works whether or not sibling tests created it.
        // History rows are stored with PG-folded (lowercase) identifiers, so filter on the folded
        // box table name and schema. The (already folded) schema qualifier is interpolated; the
        // absent-table guard short-circuits before the count touches a missing relation.
        command.CommandText = $@"
SELECT CASE WHEN EXISTS(SELECT 1 FROM information_schema.tables
                        WHERE table_schema = @SchemaName AND table_name = '__BrighterMigrationHistory')
            THEN (SELECT COUNT(1) FROM ""{schemaName}"".""__BrighterMigrationHistory""
                  WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName)
            ELSE 0 END";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var dropSchemaCmd = connection.CreateCommand();
            dropSchemaCmd.CommandText = $@"DROP SCHEMA IF EXISTS ""{_foldedSchema}"" CASCADE";
            await dropSchemaCmd.ExecuteNonQueryAsync();
            await using var dropPublicTable = connection.CreateCommand();
            dropPublicTable.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{_tableName}""";
            await dropPublicTable.ExecuteNonQueryAsync();
            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'public' AND table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""public"".""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName;
    END IF;
END
$$;";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
