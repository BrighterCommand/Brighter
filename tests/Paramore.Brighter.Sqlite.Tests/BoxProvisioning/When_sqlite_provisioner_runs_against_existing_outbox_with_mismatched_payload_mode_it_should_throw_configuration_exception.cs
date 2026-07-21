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
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

public class OutboxPayloadModeMismatchTests
{
    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_existing_outbox_body_is_text_and_provisioner_is_configured_for_binary_it_should_throw_configuration_exception()
    {
        //Arrange — text-mode outbox already exists; configure provisioner for binary.
        await CreateTable(SqliteOutboxBuilder.GetDDL(_tableName, hasBinaryMessagePayload: false));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            binaryMessagePayload: true);
        var provisioner = new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            new SqliteBoxMigrationRunner(new SqliteOutboxMigrationCatalog(), config));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        await Assert.That(exception.Message).Contains("mismatch");
    }

    [Test]
    public async Task When_existing_outbox_body_is_binary_and_provisioner_is_configured_for_text_it_should_throw_configuration_exception()
    {
        //Arrange — binary (BLOB) outbox already exists; configure provisioner for text.
        await CreateTable(SqliteOutboxBuilder.GetDDL(_tableName, hasBinaryMessagePayload: true));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            binaryMessagePayload: false);
        var provisioner = new SqliteOutboxProvisioner(
            new SqliteBoxDetectionHelper(),
            new SqliteOutboxMigrationCatalog(),
            new SqlitePayloadModeValidator(),
            config,
            new SqliteBoxMigrationRunner(new SqliteOutboxMigrationCatalog(), config));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        await Assert.That(exception.Message).Contains("mismatch");
    }

    private async Task CreateTable(string ddl)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync();
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            await command.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
