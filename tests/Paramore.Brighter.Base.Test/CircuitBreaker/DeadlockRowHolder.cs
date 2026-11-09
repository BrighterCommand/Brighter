using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public abstract class DeadlockRowHolder : IAsyncDisposable
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan s_giveUpAfter = TimeSpan.FromSeconds(20);

    protected DeadlockRowHolder(DbConnection connection, DbTransaction transaction, long blockingId)
    {
        Connection = connection;
        Transaction = transaction;
        BlockingId = blockingId;
    }

    protected DbConnection Connection { get; }

    protected DbTransaction Transaction { get; }

    protected long BlockingId { get; }

    public abstract Task TouchAsync(string topic);

    public async Task WaitUntilBlockedAsync()
    {
        var deadline = DateTime.UtcNow + s_giveUpAfter;
        while (DateTime.UtcNow < deadline)
        {
            if (await CountBlockedAsync() > 0)
            {
                await AfterBlockedAsync();
                return;
            }

            await Task.Delay(s_pollInterval);
        }

        throw new TimeoutException("Nothing blocked on the row held by the test");
    }

    public Task CommitAsync() => Transaction.CommitAsync();

    public async ValueTask DisposeAsync()
    {
        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
    }

    protected abstract Task<long> CountBlockedAsync();

    protected virtual Task AfterBlockedAsync() => Task.CompletedTask;

    protected Task ExecuteAsync(string sql) => ExecuteAsync(Connection, Transaction, sql);

    protected static async Task<(DbConnection Connection, DbTransaction Transaction)> BeginAsync(
        IAmARelationalDbConnectionProvider provider)
    {
        var connection = await provider.GetConnectionAsync(default);
        var transaction = await connection.BeginTransactionAsync();
        return (connection, transaction);
    }

    protected static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    protected static async Task<object?> ScalarAsync(DbConnection connection, DbTransaction transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
