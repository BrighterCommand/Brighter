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

namespace Paramore.Brighter.MSSQL.Tests.TestDoubles;

public sealed class MsSqlDeadlockRowHolder : DeadlockRowHolder
{
    private readonly string _table;

    private MsSqlDeadlockRowHolder(DbConnection connection, DbTransaction transaction, long sessionId, string table)
        : base(connection, transaction, sessionId)
    {
        _table = table;
    }

    public static async Task<MsSqlDeadlockRowHolder> HoldAsync(IAmARelationalDbConnectionProvider provider, string table, string topic)
    {
        var (connection, transaction) = await BeginAsync(provider);

        await ExecuteAsync(connection, transaction, "SET DEADLOCK_PRIORITY HIGH");
        await ExecuteAsync(connection, transaction, TouchSql(table, topic));
        var sessionId = Convert.ToInt64(await ScalarAsync(connection, transaction, "SELECT CAST(@@SPID AS INT)"));
        return new MsSqlDeadlockRowHolder(connection, transaction, sessionId, table);
    }

    public override Task TouchAsync(string topic) => ExecuteAsync(TouchSql(_table, topic));

    protected override async Task<long> CountBlockedAsync() =>
        Convert.ToInt64(await ScalarAsync(
            Connection, Transaction,
            $"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id = {BlockingId}"));

    private static string TouchSql(string table, string topic) =>
        $"UPDATE [{table}] SET [PriorTripTime] = [PriorTripTime] WHERE [Topic] = N'{topic}'";
}
