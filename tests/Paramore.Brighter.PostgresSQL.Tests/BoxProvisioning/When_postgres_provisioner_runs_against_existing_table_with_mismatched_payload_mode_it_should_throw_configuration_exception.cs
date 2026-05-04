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
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgres_provisioner_runs_against_existing_table_with_mismatched_payload_mode_it_should_throw_configuration_exception : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _outboxTableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _inboxTableName = $"test_inbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_throw_when_existing_outbox_body_is_text_and_provisioner_is_configured_for_binary()
    {
        //Arrange — text-mode outbox already exists; configure provisioner for binary.
        new PostgresSqlTestHelper().SetupDatabase();
        await CreateTable(PostgreSqlOutboxBuilder.GetDDL(_outboxTableName, binaryMessagePayload: false));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _outboxTableName,
            binaryMessagePayload: true);
        var provisioner = new PostgreSqlOutboxProvisioner(
            config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_throw_when_existing_outbox_body_is_bytea_and_provisioner_is_configured_for_text()
    {
        //Arrange — binary (bytea) outbox already exists; configure provisioner for text.
        new PostgresSqlTestHelper().SetupDatabase();
        await CreateTable(PostgreSqlOutboxBuilder.GetDDL(_outboxTableName, binaryMessagePayload: true));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _outboxTableName,
            binaryMessagePayload: false);
        var provisioner = new PostgreSqlOutboxProvisioner(
            config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_throw_when_existing_inbox_commandbody_is_text_and_provisioner_is_configured_for_binary()
    {
        //Arrange — text-mode inbox already exists; configure provisioner for binary.
        new PostgresSqlTestHelper().SetupDatabase();
        await CreateTable(PostgreSqlInboxBuilder.GetDDL(_inboxTableName, binaryMessagePayload: false));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _inboxTableName,
            binaryMessagePayload: true);
        var provisioner = new PostgreSqlInboxProvisioner(
            config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_throw_when_existing_inbox_commandbody_is_bytea_and_provisioner_is_configured_for_text()
    {
        //Arrange — binary (bytea) inbox already exists; configure provisioner for text.
        new PostgresSqlTestHelper().SetupDatabase();
        await CreateTable(PostgreSqlInboxBuilder.GetDDL(_inboxTableName, binaryMessagePayload: true));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            inboxTableName: _inboxTableName,
            binaryMessagePayload: false);
        var provisioner = new PostgreSqlInboxProvisioner(
            config, new PostgreSqlBoxMigrationRunner(config, TimeSpan.FromSeconds(30)));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateTable(string ddl)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS \"{_outboxTableName}\"; DROP TABLE IF EXISTS \"{_inboxTableName}\";";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
