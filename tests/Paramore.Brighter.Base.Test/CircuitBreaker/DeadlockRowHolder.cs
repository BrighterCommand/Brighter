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
