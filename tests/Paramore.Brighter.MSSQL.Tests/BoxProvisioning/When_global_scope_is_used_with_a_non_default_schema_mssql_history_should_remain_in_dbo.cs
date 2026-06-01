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

// Spec 0029 FR4/NF1/AC2/AC2a (ADR 0060 D2): per-schema history MUST NOT be inferred from
// SchemaName alone. With the DEFAULT scope (Global) and a non-null, NON-DEFAULT SchemaName, the
// box table still lands in the configured schema (F1 behaviour) but __BrighterMigrationHistory
// stays in the backend default schema (dbo) — exactly today's behaviour for operators who set
// SchemaName but never opted into PerSchema. This regression guard locks the FR1/FR4 interaction:
// a defect that placed history per-schema whenever SchemaName was set would fail here.
public class MsSqlGlobalScopeHistoryPlacementTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_global_{Guid.NewGuid():N}";
    private readonly MsSqlOutboxProvisioner _provisioner;

    public MsSqlGlobalScopeHistoryPlacementTests()
    {
        // Evident data: Global scope (the default) + a non-default SchemaName is the case under test.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.Global);
        _provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Fact]
    public async Task When_global_scope_is_used_with_a_non_default_schema_it_should_keep_history_in_dbo()
    {
        //Arrange — operator pre-creates the schema; pre-drop a per-schema history table so its
        //*absence* after provisioning is meaningful (the runner must not have created it).
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_tableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);

        //Act — fresh-install run under Global with a non-default SchemaName.
        var exception = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert — the box table lands in the configured schema (F1 behaviour, unchanged).
        Assert.Null(exception);
        Assert.True(
            TableExistsInSchema(_tableName, _schemaName),
            $"Box table must be created in the configured schema '{_schemaName}'.");

        //Assert — history is NOT inferred per-schema from SchemaName: no per-schema history table.
        Assert.False(
            TableExistsInSchema("__BrighterMigrationHistory", _schemaName),
            $"History table must NOT be created in '{_schemaName}' under Global scope.");
        Assert.Equal(0, GetHistoryRowCountInSchema(_schemaName));

        //Assert — history lives in the backend default schema (dbo), recording this tenant's
        //configured SchemaName on the row but physically resident in dbo.
        Assert.True(DboHistoryTableExists(), "[dbo].[__BrighterMigrationHistory] must exist.");
        Assert.Equal(1, GetHistoryRowCountInSchema("dbo"));
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

    private bool DboHistoryTableExists() => TableExistsInSchema("__BrighterMigrationHistory", "dbo");

    // Counts this tenant's history rows in the named physical schema's history table. Rows are
    // stamped with the box's configured SchemaName regardless of where the table physically lives,
    // so filter on that. The history table may be absent in a schema on a clean database; treat
    // "absent" as zero rows so the per-schema no-creation check works whether or not sibling tests
    // created a table there.
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

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try
        {
            DropAnyExistingTable(_tableName, _schemaName);
            DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
            DeleteDboHistoryRows();
            DropSchemaIfExists(_schemaName);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
