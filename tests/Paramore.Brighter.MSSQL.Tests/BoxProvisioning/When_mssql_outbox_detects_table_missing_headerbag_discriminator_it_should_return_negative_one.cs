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
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlBoxDiscriminatorDetectionTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_mssql_outbox_detects_table_missing_headerbag_discriminator_it_should_return_negative_one()
    {
        //Arrange — a foreign table with neither HeaderBag (outbox discriminator) nor any V1
        //columns. Helper must report -1; provisioner must refuse to touch a non-Brighter table.
        Configuration.EnsureDatabaseExists(_connectionString);
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $"CREATE TABLE [{tableName}] ([CommandId] NVARCHAR(255) NOT NULL, [Timestamp] DATETIME NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);

        //Act — direct helper call
        int detected;
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MsSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "dbo", BoxType.Outbox, migrations, default);
        }

        //Assert — helper returns -1 (no discriminator)
        Assert.Equal(-1, detected);

        //Act — provisioner end-to-end
        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlOutboxProvisioner(config, runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies this as not a Brighter outbox and names the discriminator
        Assert.Contains("not a Brighter outbox", ex.Message);
        Assert.Contains("HeaderBag", ex.Message);
    }

    [Fact]
    public async Task When_mssql_inbox_detects_table_missing_commandbody_discriminator_it_should_return_negative_one()
    {
        //Arrange — a foreign table without CommandBody (inbox discriminator).
        Configuration.EnsureDatabaseExists(_connectionString);
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $"CREATE TABLE [{tableName}] ([Topic] NVARCHAR(255) NULL, [Timestamp] DATETIME NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new MsSqlInboxMigrationCatalog().All(config);

        //Act — direct helper call
        int detected;
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MsSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "dbo", BoxType.Inbox, migrations, default);
        }

        //Assert — helper returns -1
        Assert.Equal(-1, detected);

        //Act — provisioner end-to-end
        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlInboxProvisioner(config, runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies this as not a Brighter inbox and names the discriminator
        Assert.Contains("not a Brighter inbox", ex.Message);
        Assert.Contains("CommandBody", ex.Message);
    }

    [Fact]
    public async Task When_mssql_outbox_detects_headerbag_present_but_no_v1_columns_it_should_return_zero()
    {
        //Arrange — table has HeaderBag (passes discriminator gate) but is missing other V1
        //columns like MessageId, Topic, Body. Helper must return 0; provisioner must throw.
        Configuration.EnsureDatabaseExists(_connectionString);
        var tableName = TrackTable($"unknown_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $"CREATE TABLE [{tableName}] ([Id] BIGINT NOT NULL IDENTITY, [HeaderBag] NVARCHAR(MAX) NULL, PRIMARY KEY ([Id]));");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);

        //Act — direct helper call
        int detected;
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MsSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "dbo", BoxType.Outbox, migrations, default);
        }

        //Assert — helper returns 0 (discriminator OK, but unknown schema)
        Assert.Equal(0, detected);

        //Act — provisioner end-to-end
        var runner = new MsSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30));
        var provisioner = new MsSqlOutboxProvisioner(config, runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies the table as not matching any known schema version
        Assert.Contains("does not match any known schema version", ex.Message);
    }

    [Fact]
    public async Task When_mssql_outbox_detects_v3_shaped_table_it_should_return_three()
    {
        //Arrange — V3 columns: V1 baseline + V2 (Dispatched) + V3 (CorrelationId, ReplyTo,
        //ContentType). Crucially, no PartitionKey (V4) so the helper must STOP at 3.
        Configuration.EnsureDatabaseExists(_connectionString);
        var tableName = TrackTable($"v3_outbox_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $@"CREATE TABLE [{tableName}] (
                [Id] BIGINT NOT NULL IDENTITY,
                [MessageId] NVARCHAR(255) NOT NULL,
                [Topic] NVARCHAR(255) NULL,
                [MessageType] NVARCHAR(32) NULL,
                [Timestamp] DATETIME NULL,
                [HeaderBag] NVARCHAR(MAX) NULL,
                [Body] NVARCHAR(MAX) NULL,
                [Dispatched] DATETIME NULL,
                [CorrelationId] NVARCHAR(255) NULL,
                [ReplyTo] NVARCHAR(255) NULL,
                [ContentType] NVARCHAR(128) NULL,
                PRIMARY KEY ([Id])
            );");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new MsSqlOutboxMigrationCatalog().All(config);

        //Act
        int detected;
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MsSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "dbo", BoxType.Outbox, migrations, default);
        }

        //Assert — V3 is the highest cumulative match (V4 column PartitionKey absent stops the walk)
        Assert.Equal(3, detected);
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
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            foreach (var tableName in _tablesToCleanup)
            {
                using var dropTable = connection.CreateCommand();
                dropTable.CommandText = $"DROP TABLE IF EXISTS [{tableName}]";
                dropTable.ExecuteNonQuery();
            }

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NOT NULL
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] LIKE 'foreign_%' OR [BoxTableName] LIKE 'unknown_%' OR [BoxTableName] LIKE 'v3_outbox_%'";
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
