using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Paramore.Brighter.MySQL.Tests.TestDoubles;

public sealed class DeadlockRowHolder : IAsyncDisposable
{
    private const int HEAVIER_THAN_ANY_STORE_TRANSACTION = 30;

    private readonly IAmARelationalDbConnectionProvider _provider;
    private readonly string _table;
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private ulong _transactionId;

    private DeadlockRowHolder(IAmARelationalDbConnectionProvider provider, string table, DbConnection connection, DbTransaction transaction)
    {
        _provider = provider;
        _table = table;
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<DeadlockRowHolder> HoldAsync(IAmARelationalDbConnectionProvider provider, string table, string topic)
    {
        var connection = await provider.GetConnectionAsync(default);
        var transaction = await connection.BeginTransactionAsync();
        var holder = new DeadlockRowHolder(provider, table, connection, transaction);

        for (var i = 0; i < HEAVIER_THAN_ANY_STORE_TRANSACTION; i++)
            await holder.ExecuteAsync(
                $"UPDATE `{table}` SET `PriorTripTime` = DATE_ADD('2000-01-01', INTERVAL {i} SECOND) WHERE `Topic` = '{topic}'");

        holder._transactionId = Convert.ToUInt64(await holder.ScalarAsync(
            "SELECT trx_id FROM information_schema.INNODB_TRX WHERE trx_mysql_thread_id = CONNECTION_ID()"));
        return holder;
    }

    public async Task WaitUntilBlockedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var blocked = await CountBlockedByAsync(_transactionId);
            if (blocked > 0) return;
            await Task.Delay(20);
        }

        throw new TimeoutException("Nothing blocked on the row held by the test");
    }

    public Task TouchAsync(string topic) =>
        ExecuteAsync($"UPDATE `{_table}` SET `PriorTripTime` = '2000-02-01' WHERE `Topic` = '{topic}'");

    public Task CommitAsync() => _transaction.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<long> CountBlockedByAsync(ulong transactionId)
    {
        await using var connection = await _provider.GetConnectionAsync(default);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM performance_schema.data_lock_waits WHERE BLOCKING_ENGINE_TRANSACTION_ID = {transactionId}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
