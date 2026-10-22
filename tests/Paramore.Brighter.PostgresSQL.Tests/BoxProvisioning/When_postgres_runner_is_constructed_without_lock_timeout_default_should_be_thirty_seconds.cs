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
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// A runner constructed without a lock timeout waits 30 seconds, not <see cref="TimeSpan.Zero"/> (ADR 0058 §B.5). Only the
/// detection-helper constructor makes the timeout optional; the fake lock records it, then throws to end the migration.
/// </summary>
public class PostgreSqlRunnerDefaultLockTimeoutTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_postgres_runner_is_constructed_without_lock_timeout_default_should_be_thirty_seconds()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var shortCircuit = new TimeoutException("short-circuit to read captured timeout");
        var fakeLock = new FakePostgreSqlAdvisoryLock(releaseResult: true, throwOnAcquire: shortCircuit);

        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlBoxDetectionHelper(), new PostgreSqlOutboxMigrationCatalog(), config, advisoryLock: fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await Assert.ThrowsAsync<TimeoutException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        //Assert
        Assert.Equal(TimeSpan.FromSeconds(30), fakeLock.AcquiredTimeout);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var dropTable = connection.CreateCommand();
            dropTable.CommandText = $"DROP TABLE IF EXISTS \"{_tableName}\"";
            await dropTable.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
