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

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgres_advisory_unlock_returns_false_runner_should_log_warning_and_complete_normally : IAsyncLifetime
{
    // pg_advisory_unlock returns false when the calling session does not currently hold the
    // named lock — a diagnostic anomaly because the runner just acquired it. The release
    // happens after the chain has committed, so we cannot fail the migration; we surface the
    // condition through a Warning-level log entry naming the table name and the lock key, and
    // continue. Per ADR 0057 §5b, this contract is the public boundary between the runner and
    // its IPostgreSqlAdvisoryLock collaborator.

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_log_one_warning_and_complete_migration_when_release_returns_false()
    {
        //Arrange — ensure database exists; runner uses a real Postgres connection for DDL,
        //          a fake advisory lock that returns false from Release, and a capturing
        //          logger so we can assert the warning emission.
        new PostgresSqlTestHelper().SetupDatabase();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);
        var migrations = new PostgreSqlOutboxMigrationCatalog().All(config);

        var fakeLock = new FakePostgreSqlAdvisoryLock(releaseResult: false);
        var capturingLogger = new CapturingLogger();

        var runner = new PostgreSqlBoxMigrationRunner(
            config, TimeSpan.FromSeconds(30), fakeLock, capturingLogger);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act — runner must not throw despite the false release result.
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, migrations, freshHint);

        //Assert — migration committed normally: box table created, history populated.
        Assert.Equal(1, await GetBoxTableCount());
        Assert.True(await GetHistoryRowCount() >= 1);

        //Assert — fake observed both the acquire and release calls under the expected lock key.
        //schemaName=null in MigrateAsync resolves to effectiveSchema="public" in the runner, and
        //the lock key folds the schema in (BrighterMigration_<schema>.<table>) to keep same-named
        //tables in different schemas from sharing a lock. See Item O / ADR 0057.
        var expectedLockKey = $"BrighterMigration_public.{_tableName}";
        Assert.Equal(expectedLockKey, fakeLock.AcquiredKey);
        Assert.Equal(expectedLockKey, fakeLock.ReleasedKey);

        //Assert — exactly one Warning entry whose message names both the table name and the
        //         lock key, with no exception attached (diagnostic only).
        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains(_tableName, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(expectedLockKey, warnings[0].Message, StringComparison.Ordinal);
        Assert.Null(warnings[0].Exception);
    }

    private async Task<int> GetBoxTableCount()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM information_schema.tables
WHERE table_schema = 'public' AND table_name = @TableName";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetHistoryRowCount()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(1) FROM ""public"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @TableName";
        command.Parameters.AddWithValue("@TableName", _tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"DROP TABLE IF EXISTS public.\"{_tableName}\"; " +
                $"DELETE FROM public.\"__BrighterMigrationHistory\" WHERE \"BoxTableName\" = '{_tableName}';";
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

}
