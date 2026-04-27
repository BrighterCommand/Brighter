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
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_runner_fresh_path_acquires_lock_it_should_re_check_table_existence_before_creating : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly RelationalDatabaseConfiguration _config;
    private readonly MsSqlBoxMigrationRunner _runner;

    public When_mssql_runner_fresh_path_acquires_lock_it_should_re_check_table_existence_before_creating()
    {
        _config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        _runner = new MsSqlBoxMigrationRunner(_config, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Should_re_check_table_existence_under_lock_and_fall_through_to_bootstrap()
    {
        //Arrange — simulate the TOCTOU race: another instance created the V_latest-shape outbox
        //table after detection ran but before this runner acquires the migration lock. We seed
        //the table directly + a marker row to prove preservation, then call MigrateAsync with a
        //stale BoxTableState that says "TableExists=false", as if detection was racing.
        Configuration.EnsureDatabaseExists(_connectionString);
        Configuration.CreateTable(_connectionString, SqlOutboxBuilder.GetDDL(_tableName));
        SeedMarkerRow();

        var migrations = MsSqlOutboxMigrations.All(_config);
        var staleState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — the runner must NOT throw a duplicate-CREATE-TABLE exception. The TOCTOU re-check
        //under the lock has to see that the table exists now and route to the bootstrap path
        //instead of executing V1's CREATE TABLE on the seeded table.
        var act = async () => await _runner.MigrateAsync(
            _tableName,
            schemaName: "dbo",
            BoxType.Outbox,
            migrations,
            staleState);

        var ex = await Record.ExceptionAsync(act);

        //Assert — no exception, ≥1 history row was inserted (proves bootstrap branch ran), and
        //the seeded marker row survived (no DROP/recreate happened). Specific Version values and
        //synthetic-row description text are deliberately not asserted here — Tasks 1.5 and 1.6
        //own those concerns. This test catches only the "fresh path didn't TOCTOU re-check" bug.
        Assert.Null(ex);
        Assert.True(GetHistoryRowCount() >= 1, "Bootstrap branch should have inserted at least one history row");
        Assert.Equal(1, GetMarkerRowCount());
    }

    private void SeedMarkerRow()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO [{_tableName}] ([MessageId]) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", "marker-row-must-survive");
        command.ExecuteNonQuery();
    }

    private int GetHistoryRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo'";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        return (int)command.ExecuteScalar()!;
    }

    private int GetMarkerRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{_tableName}] WHERE [MessageId] = @MessageId";
        command.Parameters.AddWithValue("@MessageId", "marker-row-must-survive");
        return (int)command.ExecuteScalar()!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            dropTable.ExecuteNonQuery();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NOT NULL
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
