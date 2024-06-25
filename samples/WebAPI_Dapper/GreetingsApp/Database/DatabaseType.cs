using System;

namespace GreetingsPorts.Database;

public enum DatabaseType
{
    MySql,
    MsSql,
    Postgres,
    Sqlite
} 

public static class DbResolver
{
    public static DatabaseType GetDatabaseType(string greetingsDbType)
    {
        return greetingsDbType switch
        {
            DatabaseGlobals.MYSQL => DatabaseType.MySql,
            DatabaseGlobals.MSSQL => DatabaseType.MsSql,
            DatabaseGlobals.POSTGRESSQL => DatabaseType.Postgres,
            DatabaseGlobals.SQLITE => DatabaseType.Sqlite,
            _ => throw new ArgumentOutOfRangeException(nameof(DatabaseGlobals.DATABASE_TYPE_ENV), "Database type is not supported")
        };
    }

}
