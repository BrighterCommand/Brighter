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
