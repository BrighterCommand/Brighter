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
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlPayloadValidatorNullSchemaTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_mssql_payload_validator_receives_null_schema_name_it_should_substitute_dbo()
    {
        // Arrange — two box-shaped tables in dbo: one with a text payload column
        // (NVARCHAR), one with a binary payload column (VARBINARY). The validator's
        // null-substitution rule (per ADR 0057 §A.1) must make a call with
        // schemaName: null behave identically to a call with schemaName: "dbo".
        // Without substitution, null would coerce to DBNull at the bind site, the
        // INFORMATION_SCHEMA query would return zero rows, dataType would be null,
        // and the mismatch arm would silently return — masking a real configuration
        // error.
        Configuration.EnsureDatabaseExists(_connectionString);

        var textTable = TrackTable($"nullschema_text_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $"CREATE TABLE [dbo].[{textTable}] ([Id] BIGINT NOT NULL IDENTITY, [Body] NVARCHAR(MAX) NULL, PRIMARY KEY ([Id]));");

        var binaryTable = TrackTable($"nullschema_binary_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $"CREATE TABLE [dbo].[{binaryTable}] ([Id] BIGINT NOT NULL IDENTITY, [Body] VARBINARY(MAX) NULL, PRIMARY KEY ([Id]));");

        var validator = new MsSqlPayloadModeValidator();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Act + Assert — text column + text mode (no mismatch): both null and "dbo" return quietly.
        await validator.ValidateAsync(
            connection, textTable, "dbo", "Body", binaryMessagePayload: false, default);
        await validator.ValidateAsync(
            connection, textTable, schemaName: null, "Body", binaryMessagePayload: false, default);

        // Act + Assert — binary column + text mode (mismatch): both null and "dbo" throw.
        // The null arm fails without substitution because the unsubstituted query
        // matches no rows and the validator returns silently.
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            validator.ValidateAsync(
                connection, binaryTable, "dbo", "Body", binaryMessagePayload: false, default));
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            validator.ValidateAsync(
                connection, binaryTable, schemaName: null, "Body", binaryMessagePayload: false, default));
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
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
