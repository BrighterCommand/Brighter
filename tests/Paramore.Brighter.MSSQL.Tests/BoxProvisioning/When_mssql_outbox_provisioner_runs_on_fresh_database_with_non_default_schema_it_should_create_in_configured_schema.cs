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
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// REQUIRES SEQUENTIAL EXECUTION — temporarily creates a non-default schema and a per-test
// outbox table inside it. DisposeAsync drops both even on test failure.
//
// Per PR #4039 reviewer item M4-1 (comment 4485697019): the SchemaName property formally
// promises that tables live in the configured schema. The pre-F1 catalog path called
// SqlOutboxBuilder.GetDDL(table, ...) which produced `CREATE TABLE Outbox` (unqualified)
// — landing the table in the connection's default schema ([dbo]) regardless of the
// SchemaName value. V2..V7 ALTER statements then targeted [{schema}].[Outbox] which did
// not exist, and second-run detection (INFORMATION_SCHEMA.COLUMNS filtered by TABLE_SCHEMA)
// looked in the configured schema, also missing the [dbo]-resident table.
//
// This integration test pins the post-F1 contract: on a fresh database with a non-default
// SchemaName, the outbox table MUST land in the configured schema, AND a second
// ProvisionAsync run MUST be a clean no-op (no error, history still has exactly one
// V_latest row, table still resident in the configured schema).
public class When_mssql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema : IAsyncLifetime
{
    private const string NonDefaultSchema = "billing_for_schema_test";
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly MsSqlOutboxProvisioner _provisioner;

    public When_mssql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema()
    {
        // Configuration explicitly carries the non-default SchemaName so the catalog's
        // FreshInstallDdl + V2..V7 ALTERs + detection helper all see the same target schema.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: NonDefaultSchema);
        var runner = new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Fact]
    public async Task Should_create_outbox_in_configured_schema_and_no_op_on_second_run()
    {
        //Arrange — the database exists and the non-default schema is pre-created. The MSSQL
        //runner does not create the box schema itself; operators provision schemas via
        //out-of-band DDL. Pre-creating [billing_for_schema_test] models that contract.
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(NonDefaultSchema);
        DropAnyExistingTable(_tableName, NonDefaultSchema);
        DropAnyExistingTable(_tableName, "dbo");

        //Act — first fresh-install run
        var firstException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert — first run succeeded, table lives in the configured schema (not dbo).
        Assert.Null(firstException);
        Assert.True(
            TableExistsInSchema(_tableName, NonDefaultSchema),
            $"Outbox table '{_tableName}' must exist in [{NonDefaultSchema}] after fresh install with SchemaName='{NonDefaultSchema}'.");
        Assert.False(
            TableExistsInSchema(_tableName, "dbo"),
            $"Outbox table '{_tableName}' must NOT exist in [dbo] when SchemaName='{NonDefaultSchema}' is configured.");

        // Migration history records a single V_latest row for the box, scoped to the
        // configured SchemaName. (The history table itself is always in [dbo] per
        // existing contract — see When_history_table_exists_in_a_non_dbo_schema_runner_*.)
        Assert.Equal(1, GetHistoryRowCount(NonDefaultSchema, _tableName));

        //Act — second run on a provisioned database
        var secondException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert — second run is a clean no-op. Table is still in the configured schema, no
        //second history row was inserted, no error was thrown.
        Assert.Null(secondException);
        Assert.True(
            TableExistsInSchema(_tableName, NonDefaultSchema),
            $"Outbox table '{_tableName}' must still exist in [{NonDefaultSchema}] after idempotent second run.");
        Assert.Equal(1, GetHistoryRowCount(NonDefaultSchema, _tableName));
    }

    private void EnsureSchemaExists(string schemaName)
    {
        ExecuteNonQuery($@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    EXEC('CREATE SCHEMA [{schemaName}]')");
    }

    private void DropSchemaIfExists(string schemaName)
    {
        ExecuteNonQuery($@"
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    DROP SCHEMA [{schemaName}]");
    }

    private void DropAnyExistingTable(string tableName, string schemaName)
    {
        ExecuteNonQuery($"DROP TABLE IF EXISTS [{schemaName}].[{tableName}]");
    }

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

    private int GetHistoryRowCount(string schemaName, string tableName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM [dbo].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName";
        command.Parameters.AddWithValue("@BoxTableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return (int)command.ExecuteScalar()!;
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
            DropAnyExistingTable(_tableName, NonDefaultSchema);
            DropAnyExistingTable(_tableName, "dbo");
            DropSchemaIfExists(NonDefaultSchema);
        }
        catch
        {
            // best-effort cleanup
        }
        return Task.CompletedTask;
    }
}
