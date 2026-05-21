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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class When_mysql_runner_runs_two_provisioners_in_distinct_schemas_they_should_not_block_each_other : IAsyncLifetime
{
    private const string DefaultDatabase = "BrighterTests";
    private const string MasterConnectionString = "Server=localhost;Uid=root;Pwd=root";

    private readonly string _billingDatabase = $"brighter_billing_{Guid.NewGuid():N}";
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_not_block_each_other()
    {
        //Arrange — two provisioners share a table name but bind to distinct MySQL databases
        //("schemas" in MySQL parlance). Provisioner A's advisory lock is wrapped with a holding
        //decorator: once A's underlying GET_LOCK succeeds, the decorator parks until releaseGate
        //fires, keeping the lock held server-side. Provisioner B has a short 1-second
        //lockTimeout. With the schema-aware fix, B's lock name folds the schema in
        //(BrighterMigration_<schema>.<table>, hash-folded if it exceeds MySQL's 64-char limit),
        //so B's name differs from A's and B completes without contention. Without the fix, both
        //names collapse to BrighterMigration_<table>, B contends with A's held GET_LOCK, and
        //B times out after 1s.
        await EnsureBillingDatabaseExists();

        var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdingLock = new HoldingMySqlAdvisoryLock(new MySqlAdvisoryLock(), releaseGate.Task);

        var configA = new RelationalDatabaseConfiguration(
            ConnectionStringFor(DefaultDatabase), outBoxTableName: _tableName);
        var configB = new RelationalDatabaseConfiguration(
            ConnectionStringFor(_billingDatabase), outBoxTableName: _tableName);

        var provisionerA = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            configA,
            new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), configA, TimeSpan.FromSeconds(30), holdingLock));
        var provisionerB = new MySqlOutboxProvisioner(
            new MySqlBoxDetectionHelper(),
            new MySqlOutboxMigrationCatalog(),
            new MySqlPayloadModeValidator(),
            configB,
            new MySqlBoxMigrationRunner(new MySqlOutboxMigrationCatalog(), configB, TimeSpan.FromSeconds(1)));

        //Act
        var taskA = Task.Run(() => provisionerA.ProvisionAsync());
        try
        {
            await holdingLock.AcquireSeen.Task; // synchronization: A has acquired GET_LOCK and is parked

            var taskBException = await Record.ExceptionAsync(() => provisionerB.ProvisionAsync());

            //Assert — B should complete without timing out on a shared lock name.
            Assert.Null(taskBException);
        }
        finally
        {
            //Cleanup A: release the gate, let A finish, regardless of B's outcome.
            releaseGate.TrySetResult(true);
            await taskA;
        }
    }

    private static string ConnectionStringFor(string database) =>
        $"Server=localhost;Uid=root;Pwd=root;Database={database}";

    private async Task EnsureBillingDatabaseExists()
    {
        await using var connection = new MySqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_billingDatabase}`";
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(MasterConnectionString);
            await connection.OpenAsync();

            await using var dropDefaultTable = connection.CreateCommand();
            dropDefaultTable.CommandText = $"DROP TABLE IF EXISTS `{DefaultDatabase}`.`{_tableName}`";
            await dropDefaultTable.ExecuteNonQueryAsync();

            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText =
                $"DELETE FROM `{DefaultDatabase}`.`__BrighterMigrationHistory` WHERE `BoxTableName` = @TableName";
            deleteHistory.Parameters.AddWithValue("@TableName", _tableName);
            try { await deleteHistory.ExecuteNonQueryAsync(); }
            catch (MySqlException) { /* history table may not exist if A rolled back before creating it */ }

            await using var dropBillingDb = connection.CreateCommand();
            dropBillingDb.CommandText = $"DROP DATABASE IF EXISTS `{_billingDatabase}`";
            await dropBillingDb.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
