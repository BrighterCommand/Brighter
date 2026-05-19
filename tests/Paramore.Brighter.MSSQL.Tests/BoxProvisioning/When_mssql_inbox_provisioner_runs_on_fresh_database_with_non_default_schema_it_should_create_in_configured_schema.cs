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

// Inbox companion to When_mssql_outbox_provisioner_runs_on_fresh_database_with_non_default_schema_*.
// Per PR #4039 reviewer item M4-1 (F1a): SqlInboxBuilder.GetDDL is now schema-aware via an
// optional schemaName parameter, and MsSqlInboxMigrationCatalog.FreshInstallDdl threads
// configuration.SchemaName through to it. This test pins the inbox half of the contract.
public class When_mssql_inbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema : IAsyncLifetime
{
    private const string NonDefaultSchema = "billing_for_inbox_schema_test";
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_inbox_{Guid.NewGuid():N}";
    private readonly MsSqlInboxProvisioner _provisioner;

    public When_mssql_inbox_provisioner_runs_on_fresh_database_with_non_default_schema_it_should_create_in_configured_schema()
    {
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _tableName,
            schemaName: NonDefaultSchema);
        var runner = new MsSqlBoxMigrationRunner(new MsSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new MsSqlInboxProvisioner(config, runner);
    }

    [Fact]
    public async Task Should_create_inbox_in_configured_schema_and_no_op_on_second_run()
    {
        //Arrange — operator pre-creates the schema; runner does not create schemas itself.
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(NonDefaultSchema);
        DropAnyExistingTable(_tableName, NonDefaultSchema);
        DropAnyExistingTable(_tableName, "dbo");

        //Act — first fresh-install run
        var firstException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert — table lives in configured schema, NOT dbo
        Assert.Null(firstException);
        Assert.True(TableExistsInSchema(_tableName, NonDefaultSchema));
        Assert.False(TableExistsInSchema(_tableName, "dbo"));
        Assert.Equal(1, GetHistoryRowCount(NonDefaultSchema, _tableName));

        //Act — second run idempotency
        var secondException = await Record.ExceptionAsync(() => _provisioner.ProvisionAsync());

        //Assert
        Assert.Null(secondException);
        Assert.True(TableExistsInSchema(_tableName, NonDefaultSchema));
        Assert.Equal(1, GetHistoryRowCount(NonDefaultSchema, _tableName));
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
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
