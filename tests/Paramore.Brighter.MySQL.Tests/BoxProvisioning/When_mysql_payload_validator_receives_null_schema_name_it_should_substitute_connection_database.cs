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
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// A null <c>schemaName</c> passed to the MySQL payload validator is substituted with
/// <c>connection.Database</c>, so a payload-mode mismatch still throws; unsubstituted, the null binds as
/// <c>DBNull</c>, the type query matches no rows and the mismatch passes silently (ADR 0058 §A.3).
/// </summary>
public class MySqlPayloadValidatorNullSchemaTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_mysql_payload_validator_receives_null_schema_name_it_should_substitute_connection_database()
    {
        // Arrange
        var textTable = TrackTable($"nullschema_text_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE `{textTable}` (`Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, `Body` LONGTEXT NULL) ENGINE = InnoDB;");

        var binaryTable = TrackTable($"nullschema_binary_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE `{binaryTable}` (`Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY, `Body` LONGBLOB NULL) ENGINE = InnoDB;");

        var databaseName = DatabaseName();
        var validator = new MySqlPayloadModeValidator();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Act + Assert
        await validator.ValidateAsync(
            connection, textTable, databaseName, "Body", binaryMessagePayload: false, default);
        await validator.ValidateAsync(
            connection, textTable, schemaName: null, "Body", binaryMessagePayload: false, default);

        // Act + Assert
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            validator.ValidateAsync(
                connection, binaryTable, databaseName, "Body", binaryMessagePayload: false, default));
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            validator.ValidateAsync(
                connection, binaryTable, schemaName: null, "Body", binaryMessagePayload: false, default));
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
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
