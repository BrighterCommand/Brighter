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
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// REQUIRES SEQUENTIAL EXECUTION (see PROMPT.md / branch convention) — temporarily drops the
// shared [dbo].[__BrighterMigrationHistory] table to demonstrate the schema_id-filter bug.
// DisposeAsync restores it (and the rest of the seeded state) even on test failure so the
// rest of the BoxProvisioning suite is left in a consistent state.
public class When_history_table_exists_in_a_non_dbo_schema_runner_should_still_create_it_in_dbo : IAsyncLifetime
{
    private const string CollidingSchema = "stage_for_history_clash_test";
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly MsSqlOutboxProvisioner _provisioner;

    public When_history_table_exists_in_a_non_dbo_schema_runner_should_still_create_it_in_dbo()
    {
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var runner = new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        _provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Fact]
    public async Task Should_filter_history_existence_check_by_schema_id()
    {
        //Arrange — pre-create [stage].[__BrighterMigrationHistory] with a deliberately wrong
        //shape. Without a schema_id filter on the runner's IF NOT EXISTS check, the runner sees
        //this as "history table already exists" and skips creating [dbo].[__BrighterMigrationHistory],
        //which then breaks the subsequent INSERT against an unqualified table reference.
        Configuration.EnsureDatabaseExists(_connectionString);
        DropDboHistoryTable();
        DropCollidingArtefacts();
        CreateCollidingSchemaAndHistoryTable();

        //Act
        var act = async () => await _provisioner.ProvisionAsync();
        var ex = await Record.ExceptionAsync(act);

        //Assert — runner must succeed, [dbo] history table must exist with the correct shape
        //and one V_latest fresh-install row, and the colliding [stage] table must be untouched.
        Assert.Null(ex);
        Assert.True(DboHistoryTableExists(), "[dbo].[__BrighterMigrationHistory] must be created");
        Assert.Equal(1, GetDboHistoryRowCountForBox());
        Assert.Equal(0, GetCollidingHistoryRowCount());
    }

    private void DropDboHistoryTable()
    {
        ExecuteNonQuery("DROP TABLE IF EXISTS [dbo].[__BrighterMigrationHistory]");
    }

    private void DropCollidingArtefacts()
    {
        ExecuteNonQuery($"DROP TABLE IF EXISTS [{CollidingSchema}].[__BrighterMigrationHistory]");
        ExecuteNonQuery($@"
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{CollidingSchema}')
    DROP SCHEMA [{CollidingSchema}]");
    }

    private void CreateCollidingSchemaAndHistoryTable()
    {
        ExecuteNonQuery($@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{CollidingSchema}')
    EXEC('CREATE SCHEMA [{CollidingSchema}]')");
        ExecuteNonQuery($@"
CREATE TABLE [{CollidingSchema}].[__BrighterMigrationHistory] (
    [Bogus] INT NOT NULL
)");
    }

    private bool DboHistoryTableExists()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM sys.tables " +
            "WHERE name = '__BrighterMigrationHistory' AND schema_id = SCHEMA_ID('dbo')";
        return (int)command.ExecuteScalar()! > 0;
    }

    private int GetDboHistoryRowCountForBox()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM [dbo].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo'";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        return (int)command.ExecuteScalar()!;
    }

    private int GetCollidingHistoryRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{CollidingSchema}].[__BrighterMigrationHistory]";
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
            ExecuteNonQuery($"DROP TABLE IF EXISTS [dbo].[{_tableName}]");
            DropCollidingArtefacts();
        }
        catch
        {
            // best-effort cleanup
        }
        return Task.CompletedTask;
    }
}
