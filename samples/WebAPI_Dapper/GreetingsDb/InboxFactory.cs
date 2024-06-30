using Paramore.Brighter;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Inbox.Sqlite;

namespace GreetingsDb;

public static class InboxFactory
{
    public static IAmAnInbox MakeInbox(DatabaseType databaseType, IAmARelationalDatabaseConfiguration configuration)
    {
        return databaseType switch
        {
            DatabaseType.Sqlite => new SqliteInbox(configuration),
            DatabaseType.MySql => new MySqlInbox(configuration),
            DatabaseType.MsSql => new MsSqlInbox(configuration),
            DatabaseType.Postgres => new PostgreSqlInbox(configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported")
        };
    }
}
