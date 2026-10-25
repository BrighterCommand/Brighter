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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// The runner rejects a migration chain that is not contiguous and strictly ascending with a
/// <see cref="ConfigurationException"/> naming the offending pair, before any DDL (ADR 0057 §3).
/// </summary>
public class MsSqlRunnerNonMonotonicMigrationsTests : IAsyncLifetime
{
    private static readonly int[] s_v1V2V3ThenBackToV2 = [0, 1, 2, 1];

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public Task When_versions_contain_a_duplicate_it_should_throw() =>
        AssertMigrationListRejected(BuildList(0, 0), expectedMarker: "V1 followed by V1");

    [Fact]
    public Task When_versions_have_a_gap_it_should_throw() =>
        AssertMigrationListRejected(BuildList(0, 2), expectedMarker: "V1 followed by V3");

    [Fact]
    public Task When_versions_are_not_strictly_ascending_it_should_throw() =>
        AssertMigrationListRejected(BuildList(s_v1V2V3ThenBackToV2), expectedMarker: "V3 followed by V2");

    private IReadOnlyList<IAmABoxMigration> BuildList(params int[] indices)
    {
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realMigrations = new MsSqlOutboxMigrationCatalog().All(config);
        return indices.Select(i => realMigrations[i]).ToList();
    }

    private async Task AssertMigrationListRejected(IReadOnlyList<IAmABoxMigration> malformed, string expectedMarker)
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var malformedCatalog = new MalformedListCatalog(malformed);
        var runner = new MsSqlBoxMigrationRunner(malformedCatalog, config, TimeSpan.FromSeconds(30));
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        Assert.Contains(expectedMarker, ex.Message, StringComparison.Ordinal);

        Assert.Equal(0, GetTableCount());
    }

    private sealed class MalformedListCatalog : IAmABoxMigrationCatalog
    {
        private readonly IReadOnlyList<IAmABoxMigration> _migrations;
        public MalformedListCatalog(IReadOnlyList<IAmABoxMigration> migrations) => _migrations = migrations;
        public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration) => _migrations;
        public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration) => string.Empty;
    }

    private int GetTableCount()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName AND s.name = 'dbo'";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return (int)command.ExecuteScalar()!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
