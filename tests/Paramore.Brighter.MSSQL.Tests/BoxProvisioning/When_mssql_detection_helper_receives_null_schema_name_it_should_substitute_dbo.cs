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

public class MsSqlDetectionHelperNullSchemaTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly List<string> _tablesToCleanup = [];

    [Fact]
    public async Task When_mssql_detection_helper_receives_null_schema_name_it_should_substitute_dbo()
    {
        // Arrange — a box-shaped table in the dbo schema and a history row recorded against
        // SchemaName='dbo'. The helper's null-substitution rule (per ADR 0057 §A.1) must make
        // a call with schemaName: null behave identically to a call with schemaName: "dbo".
        Configuration.EnsureDatabaseExists(_connectionString);
        var tableName = TrackTable($"nullschema_{Guid.NewGuid():N}");
        Configuration.CreateTable(_connectionString,
            $"CREATE TABLE [dbo].[{tableName}] ([Id] BIGINT NOT NULL IDENTITY, [HeaderBag] NVARCHAR(MAX) NULL, PRIMARY KEY ([Id]));");

        EnsureHistoryTable();
        SeedHistoryRow(tableName, schemaName: "dbo", migrationVersion: 3);

        var helper = new MsSqlBoxDetectionHelper();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Act + Assert — DoesTableExistAsync: null and "dbo" must agree, and both must find the table.
        var existsWithDbo = await helper.DoesTableExistAsync(connection, tableName, "dbo");
        var existsWithNull = await helper.DoesTableExistAsync(connection, tableName, schemaName: null);
        Assert.True(existsWithDbo);
        Assert.True(existsWithNull);

        // Act + Assert — DoesHistoryExistAsync: null must locate the row recorded against 'dbo'.
        var historyWithDbo = await helper.DoesHistoryExistAsync(connection, tableName, "dbo");
        var historyWithNull = await helper.DoesHistoryExistAsync(connection, tableName, schemaName: null);
        Assert.True(historyWithDbo);
        Assert.True(historyWithNull);

        // Act + Assert — GetMaxVersionAsync: null must read the same version recorded under 'dbo'.
        var maxWithDbo = await helper.GetMaxVersionAsync(connection, tableName, "dbo");
        var maxWithNull = await helper.GetMaxVersionAsync(connection, tableName, schemaName: null);
        Assert.Equal(3, maxWithDbo);
        Assert.Equal(3, maxWithNull);

        // Act + Assert — GetTableColumnsAsync: null must return the same column set as 'dbo'.
        var colsWithDbo = await helper.GetTableColumnsAsync(connection, tableName, "dbo");
        var colsWithNull = await helper.GetTableColumnsAsync(connection, tableName, schemaName: null);
        Assert.Contains("HeaderBag", colsWithDbo);
        Assert.Contains("HeaderBag", colsWithNull);
    }

    private string TrackTable(string tableName)
    {
        _tablesToCleanup.Add(tableName);
        return tableName;
    }

    private void EnsureHistoryTable()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__BrighterMigrationHistory')
BEGIN
    CREATE TABLE [__BrighterMigrationHistory] (
        [MigrationVersion] INT NOT NULL,
        [SchemaName] VARCHAR(256) NOT NULL DEFAULT 'dbo',
        [BoxTableName] VARCHAR(256) NOT NULL,
        [Description] NVARCHAR(512) NOT NULL,
        [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_BrighterMigrationHistory
            PRIMARY KEY ([SchemaName], [BoxTableName], [MigrationVersion])
    );
END";
        command.ExecuteNonQuery();
    }

    private void SeedHistoryRow(string boxTableName, string schemaName, int migrationVersion)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO [__BrighterMigrationHistory] ([MigrationVersion], [SchemaName], [BoxTableName], [Description])
VALUES (@MigrationVersion, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@MigrationVersion", migrationVersion);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@BoxTableName", boxTableName);
        command.Parameters.AddWithValue("@Description", "spec 0028 phase 2.1 null-substitution test");
        command.ExecuteNonQuery();
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
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] LIKE 'nullschema_%'";
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
