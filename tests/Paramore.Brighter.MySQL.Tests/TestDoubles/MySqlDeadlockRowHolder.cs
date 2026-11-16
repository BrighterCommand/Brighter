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
using Paramore.Brighter.Base.Test.CircuitBreaker;

namespace Paramore.Brighter.MySQL.Tests.TestDoubles;

public sealed class MySqlDeadlockRowHolder : DeadlockRowHolder
{
    private const int HEAVIER_THAN_ANY_STORE_TRANSACTION = 30;

    private readonly IAmARelationalDbConnectionProvider _provider;
    private readonly string _table;

    private MySqlDeadlockRowHolder(
        IAmARelationalDbConnectionProvider provider,
        DbConnection connection,
        DbTransaction transaction,
        long transactionId,
        string table)
        : base(connection, transaction, transactionId)
    {
        _provider = provider;
        _table = table;
    }

    public static async Task<MySqlDeadlockRowHolder> HoldAsync(IAmARelationalDbConnectionProvider provider, string table, string topic)
    {
        var (connection, transaction) = await BeginAsync(provider);

        for (var i = 0; i < HEAVIER_THAN_ANY_STORE_TRANSACTION; i++)
            await ExecuteAsync(connection, transaction,
                $"UPDATE `{table}` SET `PriorTripTime` = DATE_ADD('2000-01-01', INTERVAL {i} SECOND) WHERE `Topic` = '{topic}'");

        var transactionId = Convert.ToInt64(await ScalarAsync(
            connection, transaction,
            "SELECT trx_id FROM information_schema.INNODB_TRX WHERE trx_mysql_thread_id = CONNECTION_ID()"));
        return new MySqlDeadlockRowHolder(provider, connection, transaction, transactionId, table);
    }

    public override Task TouchAsync(string topic) =>
        ExecuteAsync($"UPDATE `{_table}` SET `PriorTripTime` = '2000-02-01' WHERE `Topic` = '{topic}'");

    protected override async Task<long> CountBlockedAsync()
    {
        await using var connection = await _provider.GetConnectionAsync(default);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT COUNT(*) FROM performance_schema.data_lock_waits WHERE BLOCKING_ENGINE_TRANSACTION_ID = {BlockingId}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
