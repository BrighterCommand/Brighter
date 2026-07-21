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
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlRunnerFreshPathRecheckTests
{
    private const string MarkerMessageId = "marker-row-must-survive";

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly RelationalDatabaseConfiguration _config;
    private readonly PostgreSqlBoxMigrationRunner _runner;

    public PostgreSqlRunnerFreshPathRecheckTests()
    {
        _config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        _runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), _config, TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task When_postgres_runner_fresh_path_acquires_advisory_lock_it_should_re_check_table_existence_before_creating()
    {
        //Arrange — simulate the TOCTOU race: another instance created the V_latest-shape outbox
        //table after detection ran but before this runner acquires the migration lock. We seed
        //the table directly + a marker row to prove preservation, then call MigrateAsync with a
        //stale BoxTableState that says "TableExists=false", as if detection was racing.
        new PostgresSqlTestHelper().SetupDatabase();
        await ExecuteDdl(PostgreSqlOutboxBuilder.GetDDL(_tableName));
        await SeedMarkerRow();

        var staleState = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — the runner must NOT throw a duplicate-CREATE-TABLE exception. The TOCTOU re-check
        //under the advisory lock has to see that the table exists now and route to the bootstrap
        //path instead of executing V1's CREATE TABLE on the seeded table.
        var act = async () => await _runner.MigrateAsync(
            _tableName,
            schemaName: "public",
            BoxType.Outbox,
            staleState);

        var ex = await TestExceptionRecorder.CaptureAsync(act);

        //Assert — no exception, ≥1 history row was inserted (proves bootstrap branch ran), and
        //the seeded marker row survived (no DROP/recreate happened).
        await Assert.That(ex).IsNull();
        await Assert.That(await GetHistoryRowCount() >= 1).IsTrue().Because("Bootstrap branch should have inserted at least one history row");
        await Assert.That(await GetMarkerRowCount()).IsEqualTo(1);
    }

    private async Task ExecuteDdl(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedMarkerRow()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO \"{_tableName}\" (messageid) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> GetHistoryRowCount()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM ""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = 'public'";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> GetMarkerRowCount()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM \"{_tableName}\" WHERE messageid = @MessageId";
        command.Parameters.AddWithValue("@MessageId", MarkerMessageId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS \"{_tableName}\"";
            await dropTable.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName;
    END IF;
END
$$;";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
