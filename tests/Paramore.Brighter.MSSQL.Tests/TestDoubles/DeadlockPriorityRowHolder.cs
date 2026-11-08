using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Paramore.Brighter.MSSQL.Tests.TestDoubles;

public sealed class DeadlockPriorityRowHolder : IAsyncDisposable
{
    private readonly string _table;
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private int _sessionId;

    private DeadlockPriorityRowHolder(string table, DbConnection connection, DbTransaction transaction)
    {
        _table = table;
        _connection = connection;
        _transaction = transaction;
    }

    public static async Task<DeadlockPriorityRowHolder> HoldAsync(IAmARelationalDbConnectionProvider provider, string table, string topic)
    {
        var connection = await provider.GetConnectionAsync(default);
        var transaction = await connection.BeginTransactionAsync();
        var holder = new DeadlockPriorityRowHolder(table, connection, transaction);

        await holder.ExecuteAsync("SET DEADLOCK_PRIORITY HIGH");
        await holder.TouchAsync(topic);
        holder._sessionId = (int)(await holder.ScalarAsync("SELECT CAST(@@SPID AS INT)"))!;
        return holder;
    }

    public async Task WaitUntilBlockedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var blocked = (int)(await ScalarAsync(
                $"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id = {_sessionId}"))!;
            if (blocked > 0) return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Nothing blocked on the row held by the test");
    }

    public Task TouchAsync(string topic) =>
        ExecuteAsync($"UPDATE [{_table}] SET [PriorTripTime] = [PriorTripTime] WHERE [Topic] = N'{topic}'");

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
