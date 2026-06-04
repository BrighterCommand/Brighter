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
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// Spec 0029 FR6/AC4 (ADR 0060 D4): under PerSchema, two tenants with distinct SchemaName values
// each get their own __BrighterMigrationHistory in their own schema, and provisioning one tenant
// leaves the other tenant's history byte-stable. Characterisation of T3's schema-aware
// write+read wiring; no prod code expected (ResolveHistorySchema() already keys off SchemaName).
public class MsSqlMultiTenantPerSchemaIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _boxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _tenantASchema = $"tenant_a_{Guid.NewGuid():N}";
    private readonly string _tenantBSchema = $"tenant_b_{Guid.NewGuid():N}";
    private readonly MsSqlOutboxProvisioner _tenantAProvisioner;
    private readonly MsSqlOutboxProvisioner _tenantBProvisioner;

    public MsSqlMultiTenantPerSchemaIsolationTests()
    {
        _tenantAProvisioner = BuildPerSchemaProvisioner(_tenantASchema);
        _tenantBProvisioner = BuildPerSchemaProvisioner(_tenantBSchema);
    }

    [Fact]
    public async Task When_two_mssql_tenants_use_per_schema_scope_each_should_get_independent_history()
    {
        //Arrange — operator pre-creates both tenant schemas; runner does not create schemas itself.
        //Evident data: two distinct SchemaName values, both PerSchema, sharing the same box table
        //name (realistic "same app, two tenants"). Each tenant's history must land in its own schema.
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_tenantASchema);
        EnsureSchemaExists(_tenantBSchema);
        DropAnyExistingTable(_boxTableName, _tenantASchema);
        DropAnyExistingTable(_boxTableName, _tenantBSchema);
        DropAnyExistingTable("__BrighterMigrationHistory", _tenantASchema);
        DropAnyExistingTable("__BrighterMigrationHistory", _tenantBSchema);

        //Act — both tenants provision under PerSchema.
        var tenantAFirst = await Record.ExceptionAsync(() => _tenantAProvisioner.ProvisionAsync());
        var tenantBFirst = await Record.ExceptionAsync(() => _tenantBProvisioner.ProvisionAsync());

        //Assert — each tenant has exactly one history row in its OWN schema, stamped with its
        //own SchemaName. The filtered COUNT is 1 only if A's row landed in A and B's in B; any
        //cross-contamination would show as 0 in the schema where the row should have been.
        Assert.Null(tenantAFirst);
        Assert.Null(tenantBFirst);
        Assert.Equal(1, GetHistoryRowCount(_tenantASchema, _tenantASchema));
        Assert.Equal(1, GetHistoryRowCount(_tenantBSchema, _tenantBSchema));

        //Capture tenant B's state to prove provisioning A again leaves B byte-stable.
        var tenantBAppliedAt = GetSingleHistoryAppliedAt(_tenantBSchema, _tenantBSchema);
        var tenantBMigrationVersion = GetSingleHistoryMigrationVersion(_tenantBSchema, _tenantBSchema);

        //Act — re-provision tenant A. PerSchema reads its OWN per-schema history, finds the box
        //already at the latest version, and applies nothing. Tenant B is not touched.
        var tenantASecond = await Record.ExceptionAsync(() => _tenantAProvisioner.ProvisionAsync());

        //Assert — A's history still has exactly one row (idempotent, pinned independently by T7);
        //B's history row count, AppliedAt, and MigrationVersion are unchanged. This is the FR6
        //independence guarantee: A's run does not write to, delete from, or touch B's history.
        Assert.Null(tenantASecond);
        Assert.Equal(1, GetHistoryRowCount(_tenantASchema, _tenantASchema));
        Assert.Equal(1, GetHistoryRowCount(_tenantBSchema, _tenantBSchema));
        Assert.Equal(tenantBAppliedAt, GetSingleHistoryAppliedAt(_tenantBSchema, _tenantBSchema));
        Assert.Equal(tenantBMigrationVersion, GetSingleHistoryMigrationVersion(_tenantBSchema, _tenantBSchema));
    }

    private MsSqlOutboxProvisioner BuildPerSchemaProvisioner(string schemaName)
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _boxTableName,
            schemaName: schemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
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

    // Filtered count: the row must live in `historySchema` AND be stamped with `expectedRowSchemaName`.
    // Returns 0 if the table doesn't exist (table missing is also a failure of placement).
    private int GetHistoryRowCount(string historySchema, string expectedRowSchemaName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        if (!HistoryTableExists(connection, historySchema)) return 0;
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT COUNT(1) FROM [{historySchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _boxTableName);
        command.Parameters.AddWithValue("@SchemaName", expectedRowSchemaName);
        return (int)command.ExecuteScalar()!;
    }

    private DateTime GetSingleHistoryAppliedAt(string historySchema, string expectedRowSchemaName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT [AppliedAt] FROM [{historySchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _boxTableName);
        command.Parameters.AddWithValue("@SchemaName", expectedRowSchemaName);
        return (DateTime)command.ExecuteScalar()!;
    }

    private int GetSingleHistoryMigrationVersion(string historySchema, string expectedRowSchemaName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT [MigrationVersion] FROM [{historySchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _boxTableName);
        command.Parameters.AddWithValue("@SchemaName", expectedRowSchemaName);
        return (int)command.ExecuteScalar()!;
    }

    private static bool HistoryTableExists(SqlConnection connection, string schemaName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE s.name = @SchemaName AND t.name = '__BrighterMigrationHistory';";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return (int)command.ExecuteScalar()! > 0;
    }

    private void ExecuteNonQuery(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try
        {
            DropAnyExistingTable(_boxTableName, _tenantASchema);
            DropAnyExistingTable(_boxTableName, _tenantBSchema);
            DropAnyExistingTable("__BrighterMigrationHistory", _tenantASchema);
            DropAnyExistingTable("__BrighterMigrationHistory", _tenantBSchema);
            DropAnyExistingTable(_boxTableName, "dbo");
            DropSchemaIfExists(_tenantASchema);
            DropSchemaIfExists(_tenantBSchema);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
