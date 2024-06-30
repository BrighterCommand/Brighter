using DbMaker;

namespace Greetings_Sweeper.Database;

public static class ConnectionResolver
{
    public static string? DbConnectionString(IConfiguration configuration)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        DatabaseType databaseType = DbResolver.GetDatabaseType(dbType);
        return databaseType switch
        {
            DatabaseType.MySql => configuration.GetConnectionString("GreetingsMySql"),
            DatabaseType.MsSql => configuration.GetConnectionString("GreetingsMsSql"),
            DatabaseType.Postgres => configuration.GetConnectionString("GreetingsPostgreSql"),
            DatabaseType.Sqlite => "Filename=Greetings.db;Cache=Shared",
            _ => throw new InvalidOperationException("Could not determine the database type")
        };
    }

    public static (DatabaseType databaseType, string? connectionString) ServerConnectionString(
        IConfiguration configuration)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        DatabaseType databaseType = DbResolver.GetDatabaseType(dbType);
        string? connectionString = databaseType switch
        {
            DatabaseType.MySql => configuration.GetConnectionString("MySqlDb"),
            DatabaseType.MsSql => configuration.GetConnectionString("MsSqlDb"),
            DatabaseType.Postgres => configuration.GetConnectionString("PostgreSqlDb"),
            DatabaseType.Sqlite => "Filename=Greetings.db;Cache=Shared",
            _ => throw new InvalidOperationException("Could not determine the database type")
        };
        return (databaseType, connectionString);
    }
}
