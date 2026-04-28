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
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.Legacy;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class When_mssql_bootstraps_legacy_v1_outbox_to_v7_it_should_complete_within_migration_lock_timeout : IAsyncLifetime
{
    private const int SeedVersion = 1;
    private static readonly TimeSpan MigrationLockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Nfr3Budget = TimeSpan.FromSeconds(5);

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_complete_v1_to_v7_bootstrap_well_under_lock_timeout()
    {
        //Arrange — seed a V1-shaped outbox (the heaviest bootstrap path: stamps V1 + applies V2..V7).
        Configuration.EnsureDatabaseExists(_connectionString);
        MsSqlOutboxLegacySeeder.SeedAtV(SeedVersion, _connectionString, _tableName);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var runner = new MsSqlBoxMigrationRunner(config, MigrationLockTimeout);
        var provisioner = new MsSqlOutboxProvisioner(config, runner);

        //Act — measure the wall-clock time of the public ProvisionAsync entry point.
        var stopwatch = Stopwatch.StartNew();
        await provisioner.ProvisionAsync();
        stopwatch.Stop();

        //Assert — completes well under the NFR-3 lock timeout (30s); 5s gives CI noise headroom.
        Assert.True(
            stopwatch.Elapsed < Nfr3Budget,
            $"V1→V7 bootstrap took {stopwatch.Elapsed.TotalMilliseconds:F0}ms, " +
            $"NFR-3 budget is {Nfr3Budget.TotalMilliseconds:F0}ms.");

        //Sanity — make sure we actually exercised the bootstrap path (not a no-op):
        // table reaches V7 shape and history shows synthetic V1 + applied V2..V7.
        Assert.Contains("DataRef", GetTableColumns());
        var rowsByVersion = GetHistoryRowsByVersion();
        Assert.Equal(ExpectedMigrationVersions.OutboxLatest, rowsByVersion.Count);
        Assert.Contains(SeedVersion, rowsByVersion);
        Assert.StartsWith($"bootstrap: detected at V{SeedVersion}", rowsByVersion[SeedVersion]);
    }

    private HashSet<string> GetTableColumns()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'";
        command.Parameters.AddWithValue("@TableName", _tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private Dictionary<int, string> GetHistoryRowsByVersion()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT [MigrationVersion], [Description] FROM [__BrighterMigrationHistory]
WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = 'dbo'
ORDER BY [MigrationVersion]";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);

        var rows = new Dictionary<int, string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows[reader.GetInt32(0)] = reader.GetString(1);
        }
        return rows;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS [{_tableName}]";
            dropTable.ExecuteNonQuery();

            using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
IF OBJECT_ID(N'[__BrighterMigrationHistory]', N'U') IS NOT NULL
    DELETE FROM [__BrighterMigrationHistory] WHERE [BoxTableName] = @BoxTableName";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            deleteHistory.ExecuteNonQuery();
        }
        catch
        {
            // Best effort cleanup
        }
        await Task.CompletedTask;
    }
}
