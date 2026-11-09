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
