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
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlBoxDiscriminatorDetectionTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_postgres_outbox_detects_table_missing_headerbag_discriminator_it_should_return_negative_one()
    {
        //Arrange — a foreign table with neither headerbag (outbox discriminator) nor V1
        //columns. Helper must report -1; provisioner must refuse to touch a non-Brighter table.
        new PostgresSqlTestHelper().SetupDatabase();
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE \"{tableName}\" (commandid varchar(255) NOT NULL, timestamp timestamptz NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);

        //Act — direct helper call.
        int detected;
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new PostgreSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "public", BoxType.Outbox, migrations, default);
        }

        //Assert — helper returns -1 (no discriminator).
        Assert.Equal(-1, detected);

        //Act — provisioner end-to-end.
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlOutboxProvisioner(config, runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies this as not a Brighter outbox and names the discriminator.
        Assert.Contains("not a Brighter outbox", ex.Message);
        Assert.Contains("headerbag", ex.Message);
    }

    [Fact]
    public async Task When_postgres_inbox_detects_table_missing_commandbody_discriminator_it_should_return_negative_one()
    {
        //Arrange — a foreign table without commandbody (inbox discriminator).
        new PostgresSqlTestHelper().SetupDatabase();
        var tableName = TrackTable($"foreign_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE \"{tableName}\" (topic varchar(255) NULL, timestamp timestamptz NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);

        //Act — direct helper call.
        int detected;
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new PostgreSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "public", BoxType.Inbox, migrations, default);
        }

        //Assert — helper returns -1.
        Assert.Equal(-1, detected);

        //Act — provisioner end-to-end.
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlInboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlInboxProvisioner(config, runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies this as not a Brighter inbox and names the discriminator.
        Assert.Contains("not a Brighter inbox", ex.Message);
        Assert.Contains("commandbody", ex.Message);
    }

    [Fact]
    public async Task When_postgres_outbox_detects_headerbag_present_but_no_v1_columns_it_should_return_zero()
    {
        //Arrange — table has headerbag (passes discriminator gate) but is missing other V1
        //columns like messageid, topic, body. Helper must return 0; provisioner must throw.
        new PostgresSqlTestHelper().SetupDatabase();
        var tableName = TrackTable($"unknown_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE \"{tableName}\" (id bigserial PRIMARY KEY, headerbag text NULL);");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);

        //Act — direct helper call.
        int detected;
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new PostgreSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "public", BoxType.Outbox, migrations, default);
        }

        //Assert — helper returns 0 (discriminator OK, but unknown schema).
        Assert.Equal(0, detected);

        //Act — provisioner end-to-end.
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30));
        var provisioner = new PostgreSqlOutboxProvisioner(config, runner);
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());

        //Assert — message identifies the table as not matching any known schema version.
        Assert.Contains("does not match any known schema version", ex.Message);
    }

    [Fact]
    public async Task When_postgres_outbox_detects_v3_shaped_table_it_should_return_three()
    {
        //Arrange — V3 columns: V1 baseline + V2 (dispatched) + V3 (correlationid, replyto,
        //contenttype). Crucially, no partitionkey (V4) so the helper must STOP at 3.
        new PostgresSqlTestHelper().SetupDatabase();
        var tableName = TrackTable($"v3_outbox_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $@"CREATE TABLE ""{tableName}"" (
                id bigserial PRIMARY KEY,
                messageid varchar(255) NOT NULL,
                topic varchar(255) NULL,
                messagetype varchar(32) NULL,
                timestamp timestamptz NULL,
                headerbag text NULL,
                body text NULL,
                dispatched timestamptz NULL,
                correlationid varchar(255) NULL,
                replyto varchar(255) NULL,
                contenttype varchar(128) NULL
            );");

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: tableName);
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);

        //Act
        int detected;
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new PostgreSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "public", BoxType.Outbox, migrations, default);
        }

        //Assert — V3 is the highest cumulative match (V4 column partitionkey absent stops the walk).
        Assert.Equal(3, detected);
    }

    [Fact]
    public async Task When_postgres_inbox_detects_v1_shaped_table_it_should_return_one()
    {
        //Arrange — Postgres inbox V1 column set with single-entry migration list. Detection
        //must work with a 1-entry list (Postgres inbox is V1-only by design).
        new PostgresSqlTestHelper().SetupDatabase();
        var tableName = TrackTable($"v1_inbox_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $@"CREATE TABLE ""{tableName}"" (
                commandid varchar(256) NOT NULL,
                commandtype varchar(256) NULL,
                commandbody text NULL,
                timestamp timestamptz NULL,
                contextkey varchar(256) NOT NULL,
                PRIMARY KEY (commandid, contextkey)
            );");

        var config = new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName);
        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);

        //Act
        int detected;
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            detected = await new PostgreSqlBoxDetectionHelper().DetectCurrentVersionAsync(
                connection, tableName, "public", BoxType.Inbox, migrations, default);
        }

        //Assert — V1 is the only and highest match.
        Assert.Equal(1, detected);
    }

    private async Task ExecuteDdl(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
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
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var tableName in _tablesToCleanup)
            {
                await using var dropTable = connection.CreateCommand();
                dropTable.CommandText = $"DROP TABLE IF EXISTS \"{tableName}\"";
                await dropTable.ExecuteNonQueryAsync();
            }

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""__BrighterMigrationHistory""
        WHERE ""BoxTableName"" LIKE 'foreign_%'
           OR ""BoxTableName"" LIKE 'unknown_%'
           OR ""BoxTableName"" LIKE 'v3_outbox_%'
           OR ""BoxTableName"" LIKE 'v1_inbox_%';
    END IF;
END
$$;";
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
