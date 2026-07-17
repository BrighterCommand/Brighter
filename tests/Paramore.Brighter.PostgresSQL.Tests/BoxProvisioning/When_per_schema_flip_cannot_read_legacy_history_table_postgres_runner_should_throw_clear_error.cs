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

// Spec 0029 FR5 / AC5 (ADR 0060 D5 hardening, reviewer #1): the D5 seed copies legacy "public"
// history rows into the per-schema history on every PerSchema provision run (the per-row NOT EXISTS
// PK guard makes steady-state runs a zero-row no-op — but the SELECT against the legacy table runs
// every time, so the permission is needed for the lifetime of the deployment, not just the first
// flip). Multi-tenant deployments routinely run the provisioner under tenant-isolated roles that have NO SELECT on
// "public"."__BrighterMigrationHistory". Silently absorbing the failed seed would leave an empty
// per-schema history; the runner would then bootstrap-stamp a fresh row from the box-table columns
// on the next run, effectively re-applying the migration ledger and breaking FR5. The runner must
// therefore surface the failure as a ConfigurationException naming the cause, roll the surrounding
// transaction back, and leave NO empty per-schema stub behind that would defeat FR5 on retry.
//
// Test arrangement (least-privilege credential setup, kept SYMMETRIC with the MSSQL T-PERM test
// so both backends pin the same operator scenario):
//   - ROLE created WITH LOGIN; GRANT CONNECT on the database.
//   - GRANT USAGE, CREATE on the tenant schema, plus GRANT SELECT, INSERT, UPDATE, DELETE on ALL
//     TABLES in the tenant schema and ALTER DEFAULT PRIVILEGES IN SCHEMA <tenant> for future
//     tables. The role is therefore a fully-fledged operator within its tenant — equivalent to
//     MSSQL's `GRANT CONTROL ON SCHEMA::[tenant]`.
//   - GRANT REFERENCES ON public."__BrighterMigrationHistory" so the legacy table is visible in
//     information_schema.tables (information_schema filters to relations on which the role has
//     SOME privilege; without an accompanying grant the seed's existence probe would silently
//     report "no legacy" and skip, which is a separate gap not under test here).
//   - DO NOT grant SELECT on public."__BrighterMigrationHistory" so any read against the legacy
//     history table fails with PostgresException SQLState 42501 (insufficient_privilege). The
//     runner must convert that failure into a ConfigurationException with the documented message.
public class PostgreSqlPerSchemaFlipPermissionTests
{
    private readonly string _elevatedConnectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_perm_{Guid.NewGuid():N}";
    private readonly string _foldedSchema;
    private readonly string _restrictedRole = $"brighter_restricted_{Guid.NewGuid():N}".Substring(0, 32);
    private readonly string _restrictedPassword = $"restricted_{Guid.NewGuid():N}";
    private string? _restrictedConnectionString;

    public PostgreSqlPerSchemaFlipPermissionTests()
    {
        _foldedSchema = _schemaName.ToLowerInvariant();
    }

    [Test]
    public async Task When_per_schema_flip_cannot_read_legacy_history_table_postgres_runner_should_throw_clear_error()
    {
        //Arrange — clean slate under elevated creds; operator pre-creates the (folded) tenant schema.
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_foldedSchema);
        await DropAnyExistingTableAsync(_tableName, _foldedSchema);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _foldedSchema);
        await DeletePublicHistoryRowsAsync();

        //Arrange — provision under Global with ELEVATED creds; populates "public"."__BrighterMigrationHistory"
        //with one row for this tenant. This is the legacy history the seed must copy on the flip.
        var globalProvisioner = BuildOutboxProvisioner(_elevatedConnectionString, MigrationHistoryScope.Global);
        await globalProvisioner.ProvisionAsync();
        await Assert.That(await GetHistoryRowCountInSchemaAsync("public")).IsEqualTo(1);

        //Arrange — create the restricted role with the minimum privileges documented at the top of
        //this file: enough to run a PerSchema provision against the tenant schema, but no SELECT on
        //the legacy "public"."__BrighterMigrationHistory". The runner must convert any read failure
        //on the legacy table into a ConfigurationException.
        _restrictedConnectionString = await CreateRestrictedRoleAndConnectionStringAsync();

        //Act — flip to PerSchema as the restricted role.
        var perSchemaProvisioner = BuildOutboxProvisioner(_restrictedConnectionString, MigrationHistoryScope.PerSchema);
        var exception = await TestExceptionRecorder.CaptureAsync(() => perSchemaProvisioner.ProvisionAsync());

        //Assert — the runner surfaces the failure as a ConfigurationException, NOT a raw PostgresException.
        //The documented message phrase pins the user-facing cause (operators read this and grant SELECT
        //on the legacy table); the inner exception preserves the provider error for diagnostics.
        var configException = await Assert.That(exception).IsTypeOf<ConfigurationException>();
        await Assert.That(configException.Message).Contains("every provision run reads the legacy default-schema history table");
        await Assert.That(configException.InnerException).IsNotNull();

        //Assert — legacy "public"."__BrighterMigrationHistory" row is left intact: the seed is INSERT-only
        //into the per-schema target and the transaction roll-back undoes nothing on the legacy side.
        await Assert.That(await GetHistoryRowCountInSchemaAsync("public")).IsEqualTo(1);

        //Assert — per-schema "<tenant>"."__BrighterMigrationHistory" must NOT be left as an empty stub.
        //The CREATE TABLE and the failing seed both run inside the same provisioning transaction, so
        //the roll-back unwinds the CREATE. After the PR #4155 multi-box-flip fix the seed runs on
        //every PerSchema provision, so even with an empty per-schema stub the next provision would
        //re-attempt the seed and ConfigurationException-throw again with the same permission error —
        //but leaving an empty stub still misleads operators inspecting the schema after a failed
        //flip ("history table appears provisioned"), so the rollback remains part of the contract.
        await Assert.That(await TableExistsInSchemaAsync("__BrighterMigrationHistory", _foldedSchema)).IsFalse().Because($"Per-schema history table must not be left as an empty stub in '{_foldedSchema}' after a failed flip.");
    }

    private PostgreSqlOutboxProvisioner BuildOutboxProvisioner(string connectionString, MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            connectionString,
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

    // Builds a least-privilege role (see file-header note for the rationale) and returns a connection
    // string bound to it. Runs under elevated creds. Idempotent on cleanup via DisposeAsync.
    private async Task<string> CreateRestrictedRoleAndConnectionStringAsync()
    {
        var elevatedBuilder = new NpgsqlConnectionStringBuilder(_elevatedConnectionString);
        var databaseName = elevatedBuilder.Database;

        await using (var connection = new NpgsqlConnection(_elevatedConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            // The role gets schema-level USAGE/CREATE PLUS table-level CRUD on existing AND future
            // tables in the tenant schema, mirroring an operator pattern where the tenant role is a
            // fully-fledged owner within its own schema. The only intentional gap is SELECT on the
            // legacy public.__BrighterMigrationHistory — that's the failure mode T-PERM pins.
            command.CommandText = $@"
CREATE ROLE ""{_restrictedRole}"" WITH LOGIN PASSWORD '{_restrictedPassword}';
GRANT CONNECT ON DATABASE ""{databaseName}"" TO ""{_restrictedRole}"";
GRANT USAGE ON SCHEMA public TO ""{_restrictedRole}"";
GRANT USAGE, CREATE ON SCHEMA ""{_foldedSchema}"" TO ""{_restrictedRole}"";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA ""{_foldedSchema}"" TO ""{_restrictedRole}"";
ALTER DEFAULT PRIVILEGES IN SCHEMA ""{_foldedSchema}""
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ""{_restrictedRole}"";
GRANT REFERENCES ON ""public"".""__BrighterMigrationHistory"" TO ""{_restrictedRole}"";";
            await command.ExecuteNonQueryAsync();
        }

        var restrictedBuilder = new NpgsqlConnectionStringBuilder(_elevatedConnectionString)
        {
            Username = _restrictedRole,
            Password = _restrictedPassword,
        };
        return restrictedBuilder.ConnectionString;
    }

    private async Task DropRestrictedRoleAsync()
    {
        // PostgreSQL refuses DROP ROLE while the role still owns objects or holds grants. REASSIGN
        // OWNED transfers ownership back to the elevated principal; DROP OWNED clears remaining
        // grants. Both are best-effort so cleanup never fails the test when the role was never
        // created (e.g. arrangement bailed earlier). The default-privileges ALTER from setup must
        // also be reverted, otherwise DROP OWNED can leave a stale default ACL entry behind.
        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{_restrictedRole}') THEN
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA ""{_foldedSchema}"" REVOKE ALL ON TABLES FROM ""{_restrictedRole}""';
        EXECUTE 'REASSIGN OWNED BY ""{_restrictedRole}"" TO CURRENT_USER';
        EXECUTE 'DROP OWNED BY ""{_restrictedRole}""';
        EXECUTE 'DROP ROLE ""{_restrictedRole}""';
    END IF;
END $$;";
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureSchemaExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{schemaName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropSchemaIfExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP SCHEMA IF EXISTS ""{schemaName}"" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropAnyExistingTableAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP TABLE IF EXISTS ""{schemaName}"".""{tableName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> TableExistsInSchemaAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName)";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    // Counts this tenant's history rows in the named physical schema's history table. PG plans the
    // whole statement up front and raises 42P01 if the COUNT references a missing relation — even
    // guarded by CASE/EXISTS — so probe existence in a separate round-trip and short-circuit before
    // touching the table. Runs under elevated creds so the test can read what the restricted role
    // cannot.
    private async Task<long> GetHistoryRowCountInSchemaAsync(string physicalSchema)
    {
        if (!await TableExistsInSchemaAsync("__BrighterMigrationHistory", physicalSchema))
            return 0;

        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{physicalSchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    // Removes any history rows this tenant left in public.__BrighterMigrationHistory. Tolerates an
    // absent table (returns silently) by probing existence in a separate round-trip first — Npgsql's
    // @param → $N rewrite does not penetrate the PL/pgSQL body of a DO block, so a single DO-guarded
    // DELETE with parameters fails to bind. Two parameterised statements avoid that.
    private async Task DeletePublicHistoryRowsAsync()
    {
        if (!await TableExistsInSchemaAsync("__BrighterMigrationHistory", "public"))
            return;

        await using var connection = new NpgsqlConnection(_elevatedConnectionString);
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
            await DropRestrictedRoleAsync();
            await DropSchemaIfExistsAsync(_foldedSchema);
            await DropAnyExistingTableAsync(_tableName, "public");
            await DeletePublicHistoryRowsAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
