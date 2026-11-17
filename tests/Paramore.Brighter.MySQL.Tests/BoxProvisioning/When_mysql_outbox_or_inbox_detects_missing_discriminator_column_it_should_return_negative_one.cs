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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// MySQL detection returns -1 when the outbox (<c>HeaderBag</c>) or inbox (<c>CommandBody</c>) discriminator
/// is missing, 0 when the discriminator is present but the V1 columns are not, and otherwise the highest
/// cumulative version; the provisioner refuses to touch a table detected as -1 or 0.
/// </summary>
public class MySqlBoxDiscriminatorDetectionTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_mysql_outbox_detects_table_missing_headerbag_discriminator_it_should_return_negative_one()
    {
        //Arrange
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE `{tableName}` (`CommandId` VARCHAR(255) NOT NULL, `Timestamp` TIMESTAMP NULL) ENGINE = InnoDB;");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new MySqlOutboxMigrationCatalog().All(config);

        //Act
        int detected;
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MySqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, DatabaseName(), BoxType.Outbox, migrations, default);
        }

        //Assert
        Assert.Equal(-1, detected);

        //Act
        var runner = new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Contains("not a Brighter outbox", ex.Message);
        Assert.Contains("HeaderBag", ex.Message);
    }

    [Fact]
    public async Task When_mysql_inbox_detects_table_missing_commandbody_discriminator_it_should_return_negative_one()
    {
        //Arrange
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE `{tableName}` (`Topic` VARCHAR(255) NULL, `Timestamp` TIMESTAMP NULL) ENGINE = InnoDB;");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new MySqlInboxMigrationCatalog().All(config);

        //Act
        int detected;
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MySqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, DatabaseName(), BoxType.Inbox, migrations, default);
        }

        //Assert
        Assert.Equal(-1, detected);

        //Act
        var runner = new MySqlBoxMigrationRunner(new MySqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlInboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlInboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Contains("not a Brighter inbox", ex.Message);
        Assert.Contains("CommandBody", ex.Message);
    }

    [Fact]
    public async Task When_mysql_outbox_detects_headerbag_present_but_no_v1_columns_it_should_return_zero()
    {
        //Arrange
        var tableName = TrackTable($"unknown_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE `{tableName}` (`Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, `HeaderBag` TEXT NULL) ENGINE = InnoDB;");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new MySqlOutboxMigrationCatalog().All(config);

        //Act
        int detected;
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MySqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, DatabaseName(), BoxType.Outbox, migrations, default);
        }

        //Assert
        Assert.Equal(0, detected);

        //Act
        var runner = new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            config,
            runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Contains("does not match any known schema version", ex.Message);
    }

    [Fact]
    public async Task When_mysql_outbox_detects_v3_shaped_table_it_should_return_three()
    {
        //Arrange
        var tableName = TrackTable($"v3_outbox_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $@"CREATE TABLE `{tableName}` (
                `MessageId` VARCHAR(255) NOT NULL,
                `Topic` VARCHAR(255) NOT NULL,
                `MessageType` VARCHAR(32) NOT NULL,
                `Timestamp` TIMESTAMP(3) NOT NULL,
                `HeaderBag` TEXT NOT NULL,
                `Body` TEXT NOT NULL,
                `Dispatched` TIMESTAMP(3) NULL,
                `CorrelationId` VARCHAR(255) NULL,
                `ReplyTo` VARCHAR(255) NULL,
                `ContentType` VARCHAR(128) NULL,
                PRIMARY KEY (`MessageId`)
            ) ENGINE = InnoDB;");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new MySqlOutboxMigrationCatalog().All(config);

        //Act
        int detected;
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MySqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, DatabaseName(), BoxType.Outbox, migrations, default);
        }

        //Assert
        Assert.Equal(3, detected);
    }

    [Fact]
    public async Task When_mysql_inbox_detects_v1_shaped_table_it_should_return_one()
    {
        //Arrange
        var tableName = TrackTable($"v1_inbox_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $@"CREATE TABLE `{tableName}` (
                `CommandId` VARCHAR(256) NOT NULL,
                `CommandType` VARCHAR(256) NULL,
                `CommandBody` TEXT NULL,
                `Timestamp` TIMESTAMP NULL,
                PRIMARY KEY (`CommandId`)
            ) ENGINE = InnoDB;");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new MySqlInboxMigrationCatalog().All(config);

        //Act
        int detected;
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new MySqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, DatabaseName(), BoxType.Inbox, migrations, default);
        }

        //Assert
        Assert.Equal(1, detected);
    }

    private async Task ExecuteDdl(string sql)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private string TrackTable(string tableName)
    {
        _tablesToCleanup.Add(tableName);
        return tableName;
    }

    private string DatabaseName()
    {
        var builder = new MySqlConnectionStringBuilder(_connectionString);
        return builder.Database;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var tableName in _tablesToCleanup)
            {
                using var dropTable = connection.CreateCommand();
                dropTable.CommandText = $"DROP TABLE IF EXISTS `{tableName}`";
                await dropTable.ExecuteNonQueryAsync();

                using var deleteHistory = connection.CreateCommand();
                deleteHistory.CommandText =
                    "DELETE FROM `__BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName";
                deleteHistory.Parameters.AddWithValue("@BoxTableName", tableName);
                try { await deleteHistory.ExecuteNonQueryAsync(); }
                catch (MySqlException) { /* history table may not exist */ }
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
