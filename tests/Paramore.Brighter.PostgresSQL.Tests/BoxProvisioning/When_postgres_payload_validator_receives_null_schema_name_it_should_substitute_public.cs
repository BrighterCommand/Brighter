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
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlPayloadValidatorNullSchemaTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_postgres_payload_validator_receives_null_schema_name_it_should_substitute_public()
    {
        // Arrange — two box-shaped tables in public: one with a text payload column
        // (TEXT), one with a binary payload column (BYTEA). The validator's
        // null-substitution rule (per ADR 0057 §A.1) must make a call with
        // schemaName: null behave identically to a call with schemaName: "public".
        // Without substitution, Npgsql's `AddWithValue` rejects a null Value at
        // runtime (the `object value` overload is annotated non-null), so the
        // null arm throws InvalidOperationException — masking real configuration
        // errors and breaking the role contract.
        new PostgresSqlTestHelper().SetupDatabase();

        var textTable = TrackTable($"nullschema_text_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE \"{textTable}\" (id BIGSERIAL PRIMARY KEY, body TEXT NULL);");

        var binaryTable = TrackTable($"nullschema_binary_{Guid.NewGuid():N}");
        await ExecuteDdl(
            $"CREATE TABLE \"{binaryTable}\" (id BIGSERIAL PRIMARY KEY, body BYTEA NULL);");

        var validator = new PostgreSqlPayloadModeValidator();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Act + Assert — text column + text mode (no mismatch): both null and "public" return quietly.
        await validator.ValidateAsync(
            connection, textTable, "public", "body", binaryMessagePayload: false, default);
        await validator.ValidateAsync(
            connection, textTable, schemaName: null, "body", binaryMessagePayload: false, default);

        // Act + Assert — bytea column + text mode (mismatch): both null and "public" throw.
        // The null arm fails before substitution because Npgsql's strict-null AddWithValue
        // rejects the parameter outright.
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            validator.ValidateAsync(
                connection, binaryTable, "public", "body", binaryMessagePayload: false, default));
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            validator.ValidateAsync(
                connection, binaryTable, schemaName: null, "body", binaryMessagePayload: false, default));
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
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
