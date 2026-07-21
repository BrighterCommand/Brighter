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

// Spec 0029 FR4/NF1/AC2/AC2a (ADR 0060 D2): per-schema history MUST NOT be inferred from
// SchemaName alone. With the DEFAULT scope (Global) and a non-null, NON-DEFAULT SchemaName, the
// box table still lands in the configured schema (F1 behaviour) but __BrighterMigrationHistory
// stays in the backend default schema (public) — exactly today's behaviour for operators who set
// SchemaName but never opted into PerSchema. This regression guard locks the FR1/FR4 interaction:
// a defect that placed history per-schema whenever SchemaName was set would fail here.
public class PostgreSqlGlobalScopeHistoryPlacementTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    // Evident data: Global scope (the default) + a non-default SchemaName is the case under test.
    // PG folds unquoted identifiers to lowercase, so the physical schema is the lowercased form.
    private readonly string _schemaName = $"billing_global_{Guid.NewGuid():N}";
    private readonly string _foldedSchema;

    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public PostgreSqlGlobalScopeHistoryPlacementTests()
    {
        _foldedSchema = _schemaName.ToLowerInvariant();

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.Global);
        _provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_global_scope_is_used_with_a_non_default_schema_it_should_keep_history_in_public()
    {
        //Arrange — operator pre-creates the (folded) schema; pre-drop a per-schema history table so
        //its *absence* after provisioning is meaningful (the runner must not have created it).
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_foldedSchema);
        await DropAnyExistingTableAsync(_tableName, _foldedSchema);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _foldedSchema);

        //Act — fresh-install run under Global with a non-default SchemaName.
        var exception = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — the box table lands in the configured (folded) schema (F1 behaviour, unchanged).
        await Assert.That(exception).IsNull();
        await Assert.That(await TableExistsInSchemaAsync(_tableName, _foldedSchema)).IsTrue().Because($"Box table must be created in the configured schema '{_foldedSchema}'.");

        //Assert — history is NOT inferred per-schema from SchemaName: no per-schema history table.
        await Assert.That(await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema)).IsFalse().Because($"History table must NOT be created in '{_foldedSchema}' under Global scope.");
        await Assert.That(await GetHistoryRowCountInSchemaAsync(_foldedSchema)).IsEqualTo(0);

        //Assert — history lives in the backend default schema (public), recording this tenant's
        //configured SchemaName on the row but physically resident in public.
        await Assert.That(await TableExistsInSchemaAsync("__BrighterMigrationHistory", "public")).IsTrue().Because("public.__BrighterMigrationHistory must exist.");
        await Assert.That(await GetHistoryRowCountInSchemaAsync("public")).IsEqualTo(1);
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

    // Counts this tenant's history rows in the named physical schema's history table. Rows are
    // stamped with the box's configured (folded) SchemaName regardless of where the table
    // physically lives, so filter on that. The history table may be absent in a schema on a clean
    // database; treat "absent" as zero rows so the per-schema no-creation check works whether or
    // not sibling tests created a table there. PostgreSQL plans the whole statement up front and
    // raises 42P01 if the COUNT references a missing relation — even guarded by CASE/EXISTS — so
    // probe existence in a separate round-trip and short-circuit before touching the table.
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
