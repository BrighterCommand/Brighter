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

public class When_two_mssql_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row : IAsyncLifetime
{
    private const int OutboxSeedVersion = 3;
    private const string OutboxMarkerMessageId = "outbox-marker-must-survive";
    private const string InboxMarkerCommandId = "inbox-marker-must-survive";

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _outboxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _inboxTableName = $"test_inbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_produce_exactly_one_synthetic_v3_when_two_outbox_provisioners_race()
    {
        //Arrange — seed an outbox at V3 (no history row) plus a marker row to prove preservation.
        Configuration.EnsureDatabaseExists(_connectionString);
        MsSqlOutboxLegacySeeder.SeedAtV(OutboxSeedVersion, _connectionString, _outboxTableName);
        SeedOutboxMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _outboxTableName);
        var provisionerA = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30)));
        var provisionerB = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            new MsSqlBoxMigrationRunner(new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30)));

        //Act — race two provisioners against the same legacy table.
        await Task.WhenAll(provisionerA.ProvisionAsync(), provisionerB.ProvisionAsync());

        //Assert — history has exactly one synthetic V3 + one applied per V4..V7 (no duplicates).
        var rowsByVersion = GetHistoryRowsByVersion(_outboxTableName);
        Assert.Equal(ExpectedMigrationVersions.OutboxLatest - OutboxSeedVersion + 1, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(OutboxSeedVersion, rowsByVersion);
        Assert.StartsWith($"bootstrap: detected at V{OutboxSeedVersion}", syntheticDescription);

        for (var v = OutboxSeedVersion + 1; v <= ExpectedMigrationVersions.OutboxLatest; v++)
        {
            var appliedDescription = Assert.Contains(v, rowsByVersion);
            Assert.False(
                appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
                $"V{v} should be an applied migration row, not a synthetic bootstrap row " +
                $"(description was: '{appliedDescription}')");
        }

        //Assert — table ends at V7 with seeded marker preserved.
        Assert.Contains("DataRef", GetTableColumns(_outboxTableName));
        Assert.Equal(1, GetOutboxMarkerRowCount());
    }

    [Fact]
    public async Task Should_produce_exactly_one_synthetic_v1_when_two_inbox_provisioners_race()
    {
        //Arrange — seed a V1 inbox (no ContextKey, no history) plus a marker command row.
        Configuration.EnsureDatabaseExists(_connectionString);
        MsSqlInboxLegacySeeder.SeedAtV1(_connectionString, _inboxTableName);
        SeedInboxMarkerRow();

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: _inboxTableName);
        var provisionerA = new MsSqlInboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlInboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            new MsSqlBoxMigrationRunner(new MsSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30)));
        var provisionerB = new MsSqlInboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlInboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            new MsSqlBoxMigrationRunner(new MsSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30)));

        //Act — race two provisioners against the same legacy table.
        await Task.WhenAll(provisionerA.ProvisionAsync(), provisionerB.ProvisionAsync());

        //Assert — exactly one synthetic V1 + one applied V2 (no duplicates).
        var rowsByVersion = GetHistoryRowsByVersion(_inboxTableName);
        Assert.Equal(2, rowsByVersion.Count);

        var syntheticDescription = Assert.Contains(1, rowsByVersion);
        Assert.StartsWith("bootstrap: detected at V1", syntheticDescription);

        var appliedDescription = Assert.Contains(2, rowsByVersion);
        Assert.False(
            appliedDescription.StartsWith("bootstrap:", StringComparison.Ordinal),
            $"V2 should be an applied migration row, not a synthetic bootstrap row " +
            $"(description was: '{appliedDescription}')");

        //Assert — table ends at V2 with seeded marker preserved (ContextKey NULL on existing row).
        Assert.Contains("ContextKey", GetTableColumns(_inboxTableName));
        Assert.True(InboxMarkerRowExistsWithNullContextKey());
    }

    private void SeedOutboxMarkerRow()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO [{_outboxTableName}] ([MessageId]) VALUES (@MessageId)";
        command.Parameters.AddWithValue("@MessageId", OutboxMarkerMessageId);
        command.ExecuteNonQuery();
    }

    private void SeedInboxMarkerRow()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO [{_inboxTableName}] ([CommandId]) VALUES (@CommandId)";
        command.Parameters.AddWithValue("@CommandId", InboxMarkerCommandId);
        command.ExecuteNonQuery();
    }

    private HashSet<string> GetTableColumns(string tableName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'";
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private Dictionary<int, string> GetHistoryRowsByVersion(string tableName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT [MigrationVersion], [Description] FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo'
ORDER BY [MigrationVersion]";
        command.Parameters.AddWithValue("@BoxTableName", tableName);

        var rows = new Dictionary<int, string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    private int GetOutboxMarkerRowCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{_outboxTableName}] WHERE [MessageId] = @MessageId";
        command.Parameters.AddWithValue("@MessageId", OutboxMarkerMessageId);
        return (int)command.ExecuteScalar()!;
    }

    private bool InboxMarkerRowExistsWithNullContextKey()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM [{_inboxTableName}]
WHERE [CommandId] = @CommandId AND [ContextKey] IS NULL";
        command.Parameters.AddWithValue("@CommandId", InboxMarkerCommandId);
        return (int)command.ExecuteScalar()! == 1;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var dropOutbox = connection.CreateCommand();
            dropOutbox.CommandText = $"DROP TABLE IF EXISTS [{_outboxTableName}]";
            dropOutbox.ExecuteNonQuery();

            using var dropInbox = connection.CreateCommand();
            dropInbox.CommandText = $"DROP TABLE IF EXISTS [{_inboxTableName}]";
            dropInbox.ExecuteNonQuery();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NOT NULL
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] IN (@OutboxTable, @InboxTable)";
            deleteHistory.Parameters.AddWithValue("@OutboxTable", _outboxTableName);
            deleteHistory.Parameters.AddWithValue("@InboxTable", _inboxTableName);
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
