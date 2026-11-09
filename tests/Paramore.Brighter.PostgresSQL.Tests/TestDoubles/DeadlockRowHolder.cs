using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

public sealed class DeadlockRowHolder : IAsyncDisposable
{
    private readonly string _quotedTable;
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private int _processId;

    private DeadlockRowHolder(string quotedTable, DbConnection connection, DbTransaction transaction)
    {
        _quotedTable = quotedTable;
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<DeadlockRowHolder> HoldAsync(
        IAmARelationalDbConnectionProvider provider, string quotedTable, string topic)
    {
        var connection = await provider.GetConnectionAsync(default);
        var transaction = await connection.BeginTransactionAsync();
        var holder = new DeadlockRowHolder(quotedTable, connection, transaction);

        await holder.TouchAsync(topic);
        holder._processId = Convert.ToInt32(await holder.ScalarAsync("SELECT pg_backend_pid()"));
        return holder;
    }

    public async Task WaitUntilBlockedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await ScalarAsync("SELECT pg_stat_clear_snapshot()");
            var blocked = Convert.ToInt64(await ScalarAsync(
                $"SELECT count(*) FROM pg_stat_activity WHERE {_processId} = ANY(pg_blocking_pids(pid))"));
            if (blocked > 0) return;
            await Task.Delay(20);
        }

        throw new TimeoutException("Nothing blocked on the row held by the test");
    }

    public Task TouchAsync(string topic) =>
        ExecuteAsync($"UPDATE {_quotedTable} SET prior_trip_time = prior_trip_time WHERE topic = '{topic}'");

    public Task CommitAsync() => _transaction.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
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
