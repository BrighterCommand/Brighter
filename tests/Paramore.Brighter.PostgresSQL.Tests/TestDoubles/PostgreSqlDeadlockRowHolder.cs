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

namespace Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

public sealed class PostgreSqlDeadlockRowHolder : DeadlockRowHolder
{
    private static readonly TimeSpan s_storeWaitsLongest = TimeSpan.FromMilliseconds(200);

    private readonly string _quotedTable;

    private PostgreSqlDeadlockRowHolder(DbConnection connection, DbTransaction transaction, long processId, string quotedTable)
        : base(connection, transaction, processId)
    {
        _quotedTable = quotedTable;
    }

    public static async Task<PostgreSqlDeadlockRowHolder> HoldAsync(
        IAmARelationalDbConnectionProvider provider, string quotedTable, string topic)
    {
        var (connection, transaction) = await BeginAsync(provider);

        await ExecuteAsync(connection, transaction, TouchSql(quotedTable, topic));
        var processId = Convert.ToInt64(await ScalarAsync(connection, transaction, "SELECT pg_backend_pid()"));
        return new PostgreSqlDeadlockRowHolder(connection, transaction, processId, quotedTable);
    }

    public override Task TouchAsync(string topic) => ExecuteAsync(TouchSql(_quotedTable, topic));

    protected override async Task<long> CountBlockedAsync()
    {
        await ScalarAsync(Connection, Transaction, "SELECT pg_stat_clear_snapshot()");
        return Convert.ToInt64(await ScalarAsync(
            Connection, Transaction,
            $"SELECT count(*) FROM pg_stat_activity WHERE {BlockingId} = ANY(pg_blocking_pids(pid))"));
    }

    protected override Task AfterBlockedAsync() => Task.Delay(s_storeWaitsLongest);

    private static string TouchSql(string quotedTable, string topic) =>
        $"UPDATE {quotedTable} SET prior_trip_time = prior_trip_time WHERE topic = '{topic}'";
}
