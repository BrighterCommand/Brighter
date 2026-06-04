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

// Spec 0029 FR6/AC4 (ADR 0060 D4): under PerSchema, two tenants with distinct SchemaName values
// each get their own __BrighterMigrationHistory in their own schema, and provisioning one tenant
// leaves the other tenant's history byte-stable. Characterisation of T4's schema-aware
// write+read wiring; no prod code expected (ResolveHistorySchema() already keys off SchemaName).
// PG folds unquoted identifiers to lowercase, so the schemas are pre-created and addressed in
// their folded form for symmetry with T4's read side.
public class PostgreSqlMultiTenantPerSchemaIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _boxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _tenantASchema = $"tenant_a_{Guid.NewGuid():N}";
    private readonly string _tenantBSchema = $"tenant_b_{Guid.NewGuid():N}";
    private readonly string _tenantAFolded;
    private readonly string _tenantBFolded;
    private readonly PostgreSqlOutboxProvisioner _tenantAProvisioner;
    private readonly PostgreSqlOutboxProvisioner _tenantBProvisioner;

    public PostgreSqlMultiTenantPerSchemaIsolationTests()
    {
        _tenantAFolded = _tenantASchema.ToLowerInvariant();
        _tenantBFolded = _tenantBSchema.ToLowerInvariant();
        _tenantAProvisioner = BuildPerSchemaProvisioner(_tenantASchema);
        _tenantBProvisioner = BuildPerSchemaProvisioner(_tenantBSchema);
    }

    [Fact]
    public async Task When_two_postgres_tenants_use_per_schema_scope_each_should_get_independent_history()
    {
        //Arrange — operator pre-creates both folded tenant schemas; runner does not create schemas.
        //Evident data: two distinct SchemaName values, both PerSchema, sharing the same box table
        //name (realistic "same app, two tenants"). Each tenant's history must land in its own schema.
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_tenantAFolded);
        await EnsureSchemaExistsAsync(_tenantBFolded);
        await DropAnyExistingTableAsync(_boxTableName, _tenantAFolded);
        await DropAnyExistingTableAsync(_boxTableName, _tenantBFolded);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _tenantAFolded);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _tenantBFolded);

        //Act — both tenants provision under PerSchema.
        var tenantAFirst = await Record.ExceptionAsync(() => _tenantAProvisioner.ProvisionAsync());
        var tenantBFirst = await Record.ExceptionAsync(() => _tenantBProvisioner.ProvisionAsync());

        //Assert — each tenant has exactly one history row in its OWN folded schema, stamped with
        //its own folded SchemaName. The filtered COUNT is 1 only if A's row landed in A and B's
        //in B; any cross-contamination would show as 0 in the schema where the row should have been.
        Assert.Null(tenantAFirst);
        Assert.Null(tenantBFirst);
        Assert.Equal(1L, await GetHistoryRowCountAsync(_tenantAFolded, _tenantAFolded));
        Assert.Equal(1L, await GetHistoryRowCountAsync(_tenantBFolded, _tenantBFolded));

        //Capture tenant B's state to prove provisioning A again leaves B byte-stable.
        var tenantBAppliedAt = await GetSingleHistoryAppliedAtAsync(_tenantBFolded, _tenantBFolded);
        var tenantBMigrationVersion = await GetSingleHistoryMigrationVersionAsync(_tenantBFolded, _tenantBFolded);

        //Act — re-provision tenant A. PerSchema reads its OWN per-schema history, finds the box
        //already at the latest version, and applies nothing. Tenant B is not touched.
        var tenantASecond = await Record.ExceptionAsync(() => _tenantAProvisioner.ProvisionAsync());

        //Assert — A's history still has exactly one row (idempotent, pinned independently by T7);
        //B's history row count, AppliedAt, and MigrationVersion are unchanged. This is the FR6
        //independence guarantee: A's run does not write to, delete from, or touch B's history.
        Assert.Null(tenantASecond);
        Assert.Equal(1L, await GetHistoryRowCountAsync(_tenantAFolded, _tenantAFolded));
        Assert.Equal(1L, await GetHistoryRowCountAsync(_tenantBFolded, _tenantBFolded));
        Assert.Equal(tenantBAppliedAt, await GetSingleHistoryAppliedAtAsync(_tenantBFolded, _tenantBFolded));
        Assert.Equal(tenantBMigrationVersion, await GetSingleHistoryMigrationVersionAsync(_tenantBFolded, _tenantBFolded));
    }

    private PostgreSqlOutboxProvisioner BuildPerSchemaProvisioner(string schemaName)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _boxTableName,
            schemaName: schemaName);
        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
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

    private async Task DropAnyExistingTableAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP TABLE IF EXISTS ""{schemaName}"".""{tableName}""";
        await command.ExecuteNonQueryAsync();
    }

    // Filtered count: the row must live in `historySchema` AND be stamped with `expectedRowSchemaName`.
    // Probes existence in a separate round-trip first because PG raises 42P01 on a missing relation
    // even behind CASE/EXISTS (T6 lesson) — table missing is also a failure of placement.
    private async Task<long> GetHistoryRowCountAsync(string historySchema, string expectedRowSchemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        if (!await HistoryTableExistsAsync(connection, historySchema)) return 0L;
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{historySchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @ExpectedRowSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _boxTableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@ExpectedRowSchemaName", expectedRowSchemaName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    // Returns the raw AppliedAt scalar (TIMESTAMPTZ). Kept as object so the comparison is agnostic
    // about Npgsql's CLR mapping — both reads return the same type, so Assert.Equal compares fine.
    private async Task<object> GetSingleHistoryAppliedAtAsync(string historySchema, string expectedRowSchemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT ""AppliedAt"" FROM ""{historySchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @ExpectedRowSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _boxTableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@ExpectedRowSchemaName", expectedRowSchemaName);
        return (await command.ExecuteScalarAsync())!;
    }

    private async Task<int> GetSingleHistoryMigrationVersionAsync(string historySchema, string expectedRowSchemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT ""MigrationVersion"" FROM ""{historySchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @ExpectedRowSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _boxTableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@ExpectedRowSchemaName", expectedRowSchemaName);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> HistoryTableExistsAsync(NpgsqlConnection connection, string schemaName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT 1 FROM information_schema.tables
WHERE table_schema = @SchemaName AND table_name = '__BrighterMigrationHistory'";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return await command.ExecuteScalarAsync() is not null;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var dropA = connection.CreateCommand();
            dropA.CommandText = $@"DROP SCHEMA IF EXISTS ""{_tenantAFolded}"" CASCADE";
            await dropA.ExecuteNonQueryAsync();
            await using var dropB = connection.CreateCommand();
            dropB.CommandText = $@"DROP SCHEMA IF EXISTS ""{_tenantBFolded}"" CASCADE";
            await dropB.ExecuteNonQueryAsync();
            await using var dropPublicTable = connection.CreateCommand();
            dropPublicTable.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{_boxTableName}""";
            await dropPublicTable.ExecuteNonQueryAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
