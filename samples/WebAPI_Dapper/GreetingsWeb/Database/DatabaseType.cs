namespace GreetingsWeb.Database;

public static class DatabaseGlobals
{
    //environment string key
    public const string DATABASE_TYPE_ENV = "BRIGHTER_GREETINGS_DATABASE";
    
    public const string MYSQL = "MySQL";
    public const string MSSQL = "MsSQL";
    public const string POSTGRESSQL = "PostgresSQL";
}

public enum DatabaseType
{
    MySql,
    MsSql,
    Postgres
} 
