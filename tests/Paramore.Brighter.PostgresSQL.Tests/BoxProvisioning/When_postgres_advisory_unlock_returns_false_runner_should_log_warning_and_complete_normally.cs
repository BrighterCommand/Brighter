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

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// When pg_advisory_unlock returns false the chain has already committed, so the runner logs one Warning naming the table
/// and the lock key, and completes normally (ADR 0057 §5b). A null schema name folds into the lock key as <c>public</c>.
/// </summary>
public class PostgreSqlAdvisoryUnlockReturnsFalseTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Test]
    public async Task When_postgres_advisory_unlock_returns_false_runner_should_log_warning_and_complete_normally()
    {
        //Arrange
        new PostgresSqlTestHelper().SetupDatabase();

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var fakeLock = new FakePostgreSqlAdvisoryLock(releaseResult: false);
        var capturingLogger = new CapturingLogger();

        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30), fakeLock, capturingLogger);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint);

        //Assert
        await Assert.That(await GetBoxTableCount()).IsEqualTo(1);
        await Assert.That(await GetHistoryRowCount() >= 1).IsTrue();

        var expectedLockKey = $"BrighterMigration_public.{_tableName}";
        await Assert.That(fakeLock.AcquiredKey).IsEqualTo(expectedLockKey);
        await Assert.That(fakeLock.ReleasedKey).IsEqualTo(expectedLockKey);

        var warnings = capturingLogger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        await Assert.That(warnings).HasSingleItem();
        await Assert.That(warnings[0].Message).Contains(_tableName);
        await Assert.That(warnings[0].Message).Contains(expectedLockKey);
        await Assert.That(warnings[0].Exception).IsNull();
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

    [Before(HookType.Test)]

    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]

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
