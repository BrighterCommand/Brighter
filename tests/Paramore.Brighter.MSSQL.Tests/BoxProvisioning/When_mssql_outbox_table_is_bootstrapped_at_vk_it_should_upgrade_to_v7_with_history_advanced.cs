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

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlOutboxVkToV7UpgradeTests
{
    private static readonly string[] s_v7ExpectedColumns =
    [
        "Id", "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body",
        "Dispatched", "CorrelationId", "ReplyTo", "ContentType", "PartitionKey",
        "Source", "Type", "DataSchema", "Subject", "TraceParent", "TraceState", "Baggage",
        "WorkflowId", "JobId", "DataRef", "SpecVersion"
    ];

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(5)]
    [Arguments(7)]
    public async Task When_mssql_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7_with_history_advanced(int k)
    {
        //Arrange — seed an outbox at V_k (no history row) and a marker row to prove preservation.
        Configuration.EnsureDatabaseExists(_connectionString);
        MsSqlOutboxLegacySeeder.SeedAtV(k, _connectionString, _tableName);
        SeedMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var runner = new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — the table now has the full V7 column set (V_{k+1}..V7 ALTERs applied)
        var actualColumns = GetTableColumns();
        foreach (var expected in s_v7ExpectedColumns)
        {
            await Assert.That(actualColumns).Contains(expected);
        }

        //Assert — history rows: one synthetic at V_k + one applied per V_{k+1}..V7
        var rowsByVersion = GetHistoryRowsByVersion();
        await Assert.That(rowsByVersion.Count).IsEqualTo(7 - k + 1);

        await Assert.That(rowsByVersion.ContainsKey(k)).IsTrue();

        var syntheticDescription = rowsByVersion[k];
        await Assert.That(syntheticDescription).StartsWith($"bootstrap: detected at V{k}");

        for (var v = k + 1; v <= 7; v++)
        {
            await Assert.That(rowsByVersion.ContainsKey(v)).IsTrue();
            var appliedDescription = rowsByVersion[v];
            await Assert.That(appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal)).IsFalse();
        }

        //Assert — the seeded marker row survived (no DROP/recreate happened)
        await Assert.That(GetMarkerRowCount()).IsEqualTo(1);
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

    private int GetMarkerRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{_tableName}] WHERE [MessageId] = @MessageId";
        command.Parameters.AddWithValue("@MessageId", "marker-row-must-survive");
        return (int)command.ExecuteScalar()!;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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