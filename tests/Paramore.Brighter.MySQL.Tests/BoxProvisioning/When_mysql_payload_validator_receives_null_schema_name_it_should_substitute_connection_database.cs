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

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlPayloadValidatorNullSchemaTests
{
    private readonly string _connectionString = Const.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Test]
    public async Task When_mysql_payload_validator_receives_null_schema_name_it_should_substitute_connection_database()
    {
        // Arrange — two box-shaped tables in the connection's default database: one with a
        // text payload column (LONGTEXT), one with a binary payload column (LONGBLOB). The
        // validator's null-substitution rule (per ADR 0057 §A.1) must make a call with
        // schemaName: null behave identically to a call with schemaName: connection.Database
        // — MySQL has no separate schema concept, so the database name is the schema.
        // Without substitution, MySqlConnector silently coerces a null Value to DBNull, so
        // `WHERE TABLE_SCHEMA = NULL` matches no rows, dataType comes back null, and the
        // mismatch arm silently returns — masking a real configuration error. The mismatch
        // arm is the discriminator: with null + no substitution, no exception is thrown
        // when ConfigurationException is expected.
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

        // Act + Assert — text column + text mode (no mismatch): both null and database-name return quietly.
        await validator.ValidateAsync(
            connection, textTable, databaseName, "Body", binaryMessagePayload: false, default);
        await validator.ValidateAsync(
            connection, textTable, schemaName: null, "Body", binaryMessagePayload: false, default);

        // Act + Assert — longblob column + text mode (mismatch): both null and database-name throw.
        // The null arm fails before substitution because the unsubstituted query matches no
        // rows (MySqlConnector silently coerces null to DBNull) and the validator returns silently.
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

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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