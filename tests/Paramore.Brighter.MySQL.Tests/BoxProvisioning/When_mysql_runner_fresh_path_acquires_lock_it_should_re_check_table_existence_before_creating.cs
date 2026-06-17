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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlRunnerFreshPathRecheckTests : IAsyncLifetime
{
    private const string MarkerMessageId = "marker-row-must-survive";

    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly RelationalDatabaseConfiguration _config;
    private readonly MySqlBoxMigrationRunner _runner;

    public MySqlRunnerFreshPathRecheckTests()
    {
        _config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        _runner = new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), _config, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task When_mysql_runner_fresh_path_acquires_lock_it_should_re_check_table_existence_before_creating()
    {
        //Arrange — simulate the TOCTOU race: another instance created the V_latest-shape outbox
        //table after detection ran but before this runner acquires GET_LOCK. We seed the table
        //directly + a marker row to prove preservation, then call MigrateAsync with a stale
        //BoxTableState that says "TableExists=false", as if detection was racing.
        await ExecuteDdl(MySqlOutboxBuilder.GetDDL(_tableName));
        await SeedMarkerRow();

        var staleState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — the runner must NOT throw a duplicate-CREATE-TABLE exception. The TOCTOU re-check
        //under GET_LOCK has to see that the table exists now and route to the bootstrap path
        //instead of executing V1's CREATE TABLE on the seeded table.
        var act = async () => await _runner.MigrateAsync(
            _tableName,
            schemaName: null,
            BoxType.Outbox,
            staleState);

        var ex = await Record.ExceptionAsync(act);

        //Assert — no exception, ≥1 history row was inserted (proves bootstrap branch ran), and
        //the seeded marker row survived (no DROP/recreate happened).
        Assert.Null(ex);
        Assert.True(await GetHistoryRowCount() >= 1, "Bootstrap branch should have inserted at least one history row");
        Assert.Equal(1, await GetMarkerRowCount());
    }

    private async Task ExecuteDdl(string sql)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedMarkerRow()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO `{_tableName}` (`MessageId`, `Topic`, `MessageType`, `Timestamp`, `HeaderBag`, `Body`) " +
            "VALUES (@MessageId, 'topic', 'MT_EVENT', NOW(3), '{}', '{}')";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> GetHistoryRowCount()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> GetMarkerRowCount()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM `{_tableName}` WHERE `MessageId` = @MessageId";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS `{_tableName}`";
            await dropTable.ExecuteNonQueryAsync();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText =
                "DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (MySqlException) { /* history table may not exist */ }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
