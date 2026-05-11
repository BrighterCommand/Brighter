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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_runner_fresh_path_is_called_with_migrations_not_starting_at_v1_it_should_throw : IAsyncLifetime
{
    // The fresh path executes migrations[0].UpScript verbatim as the install DDL — it assumes
    // the V1 entry, whose UpScript is the live builder DDL (V_latest-shape per ADR §3 fresh-
    // install fast path). A migration list that starts at any other version (e.g. V2 because
    // the caller filtered the list, or a misordered chain) would silently install the wrong
    // schema and stamp it under V_latest. The runner now validates migrations[0].Version == 1
    // at fresh-path entry and throws ConfigurationException so the misconfiguration surfaces
    // immediately rather than corrupting state.

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_throw_configuration_exception_and_not_create_table()
    {
        //Arrange — ensure the database exists; do NOT create the box table (so fresh path triggers).
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realMigrations = new MsSqlOutboxMigrationCatalog().All(config);
        var malformedMigrations = realMigrations.Skip(1).ToList();
        Assert.Equal(2, malformedMigrations[0].Version); // sanity: first entry is V2

        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert — runner refuses to install a non-V1 entry as the V1 schema.
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, malformedMigrations, freshHint));

        Assert.Contains("V1", ex.Message, StringComparison.Ordinal);

        //Assert — fresh-path guard fired before any DDL: the box table was not created
        //(the surrounding transaction also rolls back EnsureHistoryTableAsync's create).
        Assert.Equal(0, GetTableCount());
    }

    private int GetTableCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName AND s.name = 'dbo'";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return (int)command.ExecuteScalar()!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
