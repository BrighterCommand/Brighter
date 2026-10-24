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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// A runner built without a lock timeout waits 30 seconds, not <see cref="TimeSpan.Zero"/>, which
/// <c>sp_getapplock</c> reads as "do not wait" (ADR 0058, Guards and contracts). The test uses the
/// detection-helper constructor, the only one where the timeout is optional.
/// </summary>
public class MsSqlRunnerDefaultLockTimeoutTests : IAsyncLifetime
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_mssql_runner_is_constructed_without_lock_timeout_default_should_be_thirty_seconds()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);

        var config = new RelationalDatabaseConfiguration(_connectionString, outBoxTableName: _tableName);

        var shortCircuit = new MigrationLockDeadlockException("short-circuit to read captured timeout");
        var fakeLock = new FakeMsSqlAdvisoryLock(throwOnAcquire: shortCircuit);

        var runner = new MsSqlBoxMigrationRunner(new MsSqlBoxDetectionHelper(), new MsSqlOutboxMigrationCatalog(), config, advisoryLock: fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await Assert.ThrowsAsync<MigrationLockDeadlockException>(() => runner.MigrateAsync(
            _tableName, schemaName: null, BoxType.Outbox, freshHint));

        //Assert
        Assert.Equal(TimeSpan.FromSeconds(30), fakeLock.AcquiredTimeout);
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
        }
        catch
        {
            // Best-effort cleanup
        }
        await Task.CompletedTask;
    }
}
