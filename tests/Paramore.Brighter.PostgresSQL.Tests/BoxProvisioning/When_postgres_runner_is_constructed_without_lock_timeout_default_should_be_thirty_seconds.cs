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

public class PostgreSqlRunnerDefaultLockTimeoutTests : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_postgres_runner_is_constructed_without_lock_timeout_default_should_be_thirty_seconds()
    {
        //Arrange — the runner is constructed via the detection-helper ctor with `lockTimeout`
        // OMITTED, exercising the optional-parameter default path. A fake IPostgreSqlAdvisoryLock
        // captures the timeout value flowed through MigrateAsync → BeginAsync → AcquireAsync
        // and then immediately throws to short-circuit the migration (we only care about the
        // captured timeout, not a successful round-trip). Per the cross-backend harmonisation
        // contract (PR #4039 review item #1), the default MUST be 30 seconds so that direct
        // ctor callers do not silently get TimeSpan.Zero — under PostgreSql's elapsed-time
        // loop (ADR 0058 §B.1) that value collapses to a single failed pg_try_advisory_lock
        // attempt followed by an immediate TimeoutException, an operational footgun.
        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var shortCircuit = new TimeoutException("short-circuit to read captured timeout");
        var fakeLock = new FakePostgreSqlAdvisoryLock(releaseResult: true, throwOnAcquire: shortCircuit);

        // Detection-helper ctor is the ONLY one that exposes `lockTimeout` as optional. The
        // backward-compat ctor (PostgreSqlBoxMigrationRunner.cs:76) takes it as required, so it
        // cannot exercise the default path.
        var runner = new PostgreSqlBoxMigrationRunner(new PostgreSqlBoxDetectionHelper(), new PostgreSqlOutboxMigrationCatalog(), config, advisoryLock: fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await Assert.ThrowsAsync<TimeoutException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        //Assert — the omitted `lockTimeout` resolves to the 30-second default rather than
        // TimeSpan.Zero. The default is now consistent with SqliteBoxMigrationRunner's existing
        // default (the reference value the four relational runners are being harmonised against).
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
