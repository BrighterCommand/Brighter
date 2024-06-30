using Microsoft.Extensions.Configuration;

namespace GreetingsDb;

public static class ConnectionResolver
{
    public static string? DbConnectionString(IConfiguration configuration)
    {
        var dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
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

    public static (DatabaseType databaseType, string? connectionString) ServerConnectionString(IConfiguration configuration)
    {
        var dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        var databaseType = DbResolver.GetDatabaseType(dbType);
        var connectionString = databaseType switch
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
