namespace DbMaker;

public enum Rdbms
{
    MySql,
    MsSql,
    Postgres,
    Sqlite
}

public static class DbResolver
{
    public static Rdbms GetDatabaseType(string greetingsDbType)
    {
        return greetingsDbType switch
        {
            DatabaseGlobals.MYSQL => Rdbms.MySql,
            DatabaseGlobals.MSSQL => Rdbms.MsSql,
            DatabaseGlobals.POSTGRESSQL => Rdbms.Postgres,
            DatabaseGlobals.SQLITE => Rdbms.Sqlite,
            _ => throw new ArgumentOutOfRangeException(nameof(DatabaseGlobals.DATABASE_TYPE_ENV),
                "Database type is not supported")
        };
    }
}
