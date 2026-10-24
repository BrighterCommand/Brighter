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

/// <summary>
/// When the table appears between detection and the lock, the runner's re-check under the lock
/// sends it down the bootstrap path rather than the fresh-install <c>CREATE TABLE</c>, and the
/// table's rows survive (ADR 0057 §3).
/// </summary>
public class MsSqlRunnerFreshPathRecheckTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly RelationalDatabaseConfiguration _config;
    private readonly MsSqlBoxMigrationRunner _runner;

    public MsSqlRunnerFreshPathRecheckTests()
    {
        _config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        _runner = new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), _config, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task When_mssql_runner_fresh_path_acquires_lock_it_should_re_check_table_existence_before_creating()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        Configuration.CreateTable(_connectionString, SqlOutboxBuilder.GetDDL(_tableName));
        SeedMarkerRow();

        var staleState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        var act = async () => await _runner.MigrateAsync(
            _tableName,
            schemaName: "dbo",
            BoxType.Outbox,
            staleState);

        var ex = await Record.ExceptionAsync(act);

        //Assert
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
