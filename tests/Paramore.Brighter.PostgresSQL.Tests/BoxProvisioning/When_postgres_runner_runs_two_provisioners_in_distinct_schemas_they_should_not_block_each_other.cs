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
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_postgres_runner_runs_two_provisioners_in_distinct_schemas_they_should_not_block_each_other : IAsyncLifetime
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _billingSchema = $"billing_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_not_block_each_other()
    {
        //Arrange — two provisioners share a table name but use distinct schemas (public vs. a
        //test-only billing_<guid>). Provisioner A's advisory lock is wrapped with a holding
        //decorator: once A's underlying Postgres advisory lock is acquired, the decorator parks
        //until releaseGate fires, keeping the lock held. Provisioner B has a short 1-second
        //lockTimeout. With the schema-aware fix, B's lock key is distinct from A's
        //(BrighterMigration_billing_<guid>.<table> vs BrighterMigration_public.<table>), so B
        //completes without contention. Without the fix, both keys collapse to
        //BrighterMigration_<table>, B contends with A's held lock, and B times out after 1s.
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureBillingSchemaExists();

        var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdingLock = new HoldingPostgreSqlAdvisoryLock(new PostgreSqlAdvisoryLock(), releaseGate.Task);

        var configA = new RelationalDatabaseConfiguration(
            _connectionString, outBoxTableName: _tableName);
        var configB = new RelationalDatabaseConfiguration(
            _connectionString, outBoxTableName: _tableName, schemaName: _billingSchema);

        var provisionerA = new PostgreSqlOutboxProvisioner(
            configA,
            new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), configA, TimeSpan.FromSeconds(30), holdingLock));
        var provisionerB = new PostgreSqlOutboxProvisioner(
            configB,
            new PostgreSqlBoxMigrationRunner(new PostgreSqlOutboxMigrationCatalog(), configB, TimeSpan.FromSeconds(1)));

        //Act
        var taskA = Task.Run(() => provisionerA.ProvisionAsync());
        try
        {
            await holdingLock.AcquireSeen.Task; // synchronization: A has acquired the lock and is parked

            var taskBException = await Record.ExceptionAsync(() => provisionerB.ProvisionAsync());

            //Assert — B should complete without timing out on a shared lock key.
            Assert.Null(taskBException);
        }
        finally
        {
            //Cleanup A: release the gate, let A finish, regardless of B's outcome.
            releaseGate.TrySetResult(true);
            await taskA;
        }
    }

    private async Task EnsureBillingSchemaExists()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{_billingSchema}""";
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var dropPublicTable = connection.CreateCommand();
            dropPublicTable.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{_tableName}""";
            await dropPublicTable.ExecuteNonQueryAsync();

            await using var dropBillingSchema = connection.CreateCommand();
            dropBillingSchema.CommandText = $@"DROP SCHEMA IF EXISTS ""{_billingSchema}"" CASCADE";
            await dropBillingSchema.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'public' AND table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""public"".""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = @BoxTableName;
    END IF;
END
$$;";
            deleteHistory.Parameters.AddWithValue("@BoxTableName", _tableName);
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
