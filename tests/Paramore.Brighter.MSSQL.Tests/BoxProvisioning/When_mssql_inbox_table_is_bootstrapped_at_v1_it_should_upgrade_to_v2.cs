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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.Legacy;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_inbox_table_is_bootstrapped_at_v1_it_should_upgrade_to_v2 : IAsyncLifetime
{
    private static readonly string[] s_v2ExpectedColumns =
    [
        "Id", "CommandId", "CommandType", "CommandBody", "Timestamp", "ContextKey"
    ];

    private const string MarkerCommandId = "marker-command-must-survive";

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_inbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_add_contextkey_synthetic_v1_and_applied_v2_history()
    {
        //Arrange — seed a V1 inbox (no ContextKey, no history) plus a marker command row.
        Configuration.EnsureDatabaseExists(_connectionString);
        MsSqlInboxLegacySeeder.SeedAtV1(_connectionString, _tableName);
        SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: _tableName);
        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlInboxProvisioner(config, runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — the table now has the V2 column set (ContextKey ALTER applied)
        var actualColumns = GetTableColumns();
        foreach (var expected in s_v2ExpectedColumns)
        {
            Assert.Contains(expected, actualColumns);
        }

        //Assert — history rows: one synthetic at V1 + one applied at V2
        var rowsByVersion = GetHistoryRowsByVersion();
        Assert.Equal(2, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(1, rowsByVersion);
        Assert.StartsWith("bootstrap: detected at V1", syntheticDescription);

        var appliedDescription = Assert.Contains(2, rowsByVersion);
        Assert.False(
            appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
            $"V2 should be an applied migration row, not a synthetic bootstrap row " +
            $"(description was: '{appliedDescription}')");

        //Assert — the seeded marker row survived with NULL ContextKey on the new column
        Assert.True(MarkerRowExistsWithNullContextKey());
    }

    private void SeedMarkerRow()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO [{_tableName}] ([CommandId]) VALUES (@CommandId)";
        command.Parameters.AddWithValue("@CommandId", MarkerCommandId);
        command.ExecuteNonQuery();
    }

    private HashSet<string> GetTableColumns()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'";
        command.Parameters.AddWithValue("@TableName", _tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private Dictionary<int, string> GetHistoryRowsByVersion()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT [MigrationVersion], [Description] FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo'
ORDER BY [MigrationVersion]";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);

        var rows = new Dictionary<int, string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    private bool MarkerRowExistsWithNullContextKey()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM [{_tableName}]
WHERE [CommandId] = @CommandId AND [ContextKey] IS NULL";
        command.Parameters.AddWithValue("@CommandId", MarkerCommandId);
        return (int)command.ExecuteScalar()! == 1;
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
