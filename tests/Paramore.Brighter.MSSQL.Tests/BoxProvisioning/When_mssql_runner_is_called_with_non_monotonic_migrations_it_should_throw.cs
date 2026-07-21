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

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlRunnerNonMonotonicMigrationsTests
{
    // The runner validates that the supplied migrations list is contiguous and strictly
    // ascending (each V_{i+1} == V_i + 1) at MigrateAsync entry — before path branching, so
    // the rule applies uniformly across fresh / bootstrap / normal paths. Duplicates corrupt
    // the history-table PK; gaps silently skip ALTERs that V_latest depends on; out-of-order
    // lists double-apply DDL or stamp history rows in the wrong sequence. Rejecting these
    // up-front turns silent corruption into an actionable ConfigurationException naming the
    // offending pair.
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public Task When_versions_contain_a_duplicate_it_should_throw() =>
        AssertMigrationListRejected(BuildList(0, 0), expectedMarker: "V1 followed by V1");

    [Test]
    public Task When_versions_have_a_gap_it_should_throw() =>
        AssertMigrationListRejected(BuildList(0, 2), expectedMarker: "V1 followed by V3");

    [Test]
    public Task When_versions_are_not_strictly_ascending_it_should_throw() =>
        // [V1, V2, V3, V2] — valid prefix isolates the V3→V2 descent as the sole violation.
        AssertMigrationListRejected(BuildList(0, 1, 2, 1), expectedMarker: "V3 followed by V2");

    private IReadOnlyList<IAmABoxMigration> BuildList(params int[] indices)
    {
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var realMigrations = new MsSqlOutboxMigrationCatalog().All(config);
        return indices.Select(i => realMigrations[i]).ToList();
    }

    private async Task AssertMigrationListRejected(IReadOnlyList<IAmABoxMigration> malformed, string expectedMarker)
    {
        //Arrange — ensure the database exists; do NOT create the box table (so fresh path is selected).
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var malformedCatalog = new MalformedListCatalog(malformed);
        var runner = new MsSqlBoxMigrationRunner(malformedCatalog, config, TimeSpan.FromSeconds(30));
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act + Assert — runner refuses to begin migration when the version sequence is malformed.
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        await Assert.That(ex.Message).Contains(expectedMarker);

        //Assert — guard fired before any DDL: the box table was not created.
        await Assert.That(GetTableCount()).IsEqualTo(0);
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

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
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
