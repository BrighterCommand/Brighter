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
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class SqliteBoxDiscriminatorDetectionTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_sqlite_outbox_detects_table_missing_headerbag_discriminator_it_should_return_negative_one()
    {
        //Arrange — a foreign table with neither HeaderBag (outbox discriminator) nor V1 columns.
        //Helper must report -1; provisioner must refuse to touch a non-Brighter table.
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE [{tableName}] ([CommandId] TEXT NOT NULL, [Timestamp] TEXT NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        //Act — direct helper call.
        int detected;
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new SqliteBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, null, BoxType.Outbox, migrations, default);
        }

        //Assert — helper returns -1 (no discriminator).
        Assert.Equal(-1, detected);

        //Act — provisioner end-to-end.
        var runner = new SqliteBoxMigrationRunner(new SqliteOutboxMigrationCatalog(), config);
        var provisioner = new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies this as not a Brighter outbox and names the discriminator.
        Assert.Contains("not a Brighter outbox", ex.Message);
        Assert.Contains("HeaderBag", ex.Message);
    }

    [Fact]
    public async Task When_sqlite_inbox_detects_table_missing_commandbody_discriminator_it_should_return_negative_one()
    {
        //Arrange — a foreign table without CommandBody (inbox discriminator).
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE [{tableName}] ([Topic] TEXT NULL, [Timestamp] TEXT NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new SqliteInboxMigrationCatalog().All(config);

        //Act — direct helper call.
        int detected;
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new SqliteBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, null, BoxType.Inbox, migrations, default);
        }

        //Assert — helper returns -1.
        Assert.Equal(-1, detected);

        //Act — provisioner end-to-end.
        var runner = new SqliteBoxMigrationRunner(new SqliteInboxMigrationCatalog(), config);
        var provisioner = new SqliteInboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteInboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies this as not a Brighter inbox and names the discriminator.
        Assert.Contains("not a Brighter inbox", ex.Message);
        Assert.Contains("CommandBody", ex.Message);
    }

    [Fact]
    public async Task When_sqlite_outbox_detects_headerbag_present_but_no_v1_columns_it_should_return_zero()
    {
        //Arrange — table has HeaderBag (passes discriminator gate) but is missing other V1
        //columns (MessageId, Topic, Body…). Helper must return 0; provisioner must throw.
        var tableName = TrackTable($"unknown_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE [{tableName}] ([Id] INTEGER PRIMARY KEY, [HeaderBag] TEXT NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        //Act — direct helper call.
        int detected;
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new SqliteBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, null, BoxType.Outbox, migrations, default);
        }

        //Assert — helper returns 0 (discriminator OK, but unknown schema).
        Assert.Equal(0, detected);

        //Act — provisioner end-to-end.
        var runner = new SqliteBoxMigrationRunner(new SqliteOutboxMigrationCatalog(), config);
        var provisioner = new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies the table as not matching any known schema version.
        Assert.Contains("does not match any known schema version", ex.Message);
    }

    [Fact]
    public async Task When_sqlite_outbox_detects_v3_shaped_table_it_should_return_three()
    {
        //Arrange — V3 columns: V1 baseline + V2 (Dispatched) + V3 (CorrelationId, ReplyTo,
        //ContentType). Crucially, no PartitionKey (V4) so the helper must STOP at 3.
        var tableName = TrackTable($"v3_outbox_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $@"CREATE TABLE [{tableName}] (
                [MessageId] TEXT NOT NULL COLLATE NOCASE,
                [Topic] TEXT NOT NULL,
                [MessageType] TEXT NOT NULL,
                [Timestamp] TEXT NOT NULL,
                [HeaderBag] TEXT NOT NULL,
                [Body] TEXT NOT NULL,
                [Dispatched] TEXT NULL,
                [CorrelationId] TEXT NULL,
                [ReplyTo] TEXT NULL,
                [ContentType] TEXT NULL
            );");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new SqliteOutboxMigrationCatalog().All(config);

        //Act
        int detected;
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new SqliteBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, null, BoxType.Outbox, migrations, default);
        }

        //Assert — V3 is the highest cumulative match (V4 column PartitionKey absent stops the walk).
        Assert.Equal(3, detected);
    }

    [Fact]
    public async Task When_sqlite_inbox_detects_v1_shaped_table_it_should_return_one()
    {
        //Arrange — SQLite inbox V1 column set without ContextKey. Detection must stop at V1
        //because V2 column (ContextKey) is absent.
        var tableName = TrackTable($"v1_inbox_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $@"CREATE TABLE [{tableName}] (
                [CommandId] TEXT NOT NULL,
                [CommandType] TEXT NULL,
                [CommandBody] TEXT NULL,
                [Timestamp] TEXT NULL,
                PRIMARY KEY ([CommandId])
            );");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new SqliteInboxMigrationCatalog().All(config);

        //Act
        int detected;
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new SqliteBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, null, BoxType.Inbox, migrations, default);
        }

        //Assert — V1 matches (V2 column ContextKey absent stops the walk).
        Assert.Equal(1, detected);
    }

    private async Task ExecuteDdl(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private string TrackTable(string tableName)
    {
        _tablesToCleanup.Add(tableName);
        return tableName;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var tableName in _tablesToCleanup)
            {
                await using var dropTable = connection.CreateCommand();
                dropTable.CommandText = $"DROP TABLE IF EXISTS [{tableName}]";
                await dropTable.ExecuteNonQueryAsync();

                await using var deleteHistory = connection.CreateCommand();
                deleteHistory.CommandText =
                    "DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
                deleteHistory.Parameters.AddWithValue("@BoxTableName", tableName);
                try { await deleteHistory.ExecuteNonQueryAsync(); }
                catch (SqliteException) { /* history table may not exist */ }
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
