using Microsoft.Extensions.Configuration;

namespace DbMaker;

public static class ConnectionResolver
{
    public static string? GreetingsDbConnectionString(IConfiguration configuration)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

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

    public static string? GetSalutationsDbConnectionString(IConfiguration config, DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.MySql => config.GetConnectionString("SalutationsMySql"),
            DatabaseType.MsSql => config.GetConnectionString("SalutationsMsSql"),
            DatabaseType.Postgres => config.GetConnectionString("SalutationsPostgreSql"),
            DatabaseType.Sqlite => "Filename=Salutations.db;Cache=Shared",
            _ => throw new InvalidOperationException("Could not determine the database type")
        };
    }

    public static (DatabaseType databaseType, string? connectionString) ServerConnectionString(
        IConfiguration configuration)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

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

    public static IAmazonDynamoDB CreateAndRegisterClient(IServiceCollection services, bool isLocal)
    {
        var client = isLocal ? CreateAndRegisterLocalClient(services) : CreateAndRegisterRemoteClient(services);
        return client;
    }
    
    private static IAmazonDynamoDB CreateAndRegisterLocalClient(IServiceCollection services)
    {
        var credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");
            
        var clientConfig = new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" };

        var dynamoDb = new AmazonDynamoDBClient(credentials, clientConfig);
        services.Add(new ServiceDescriptor(typeof(IAmazonDynamoDB), dynamoDb));

        return dynamoDb;
    }     
        
    private static IAmazonDynamoDB CreateAndRegisterRemoteClient(IServiceCollection services)
    {
        //you need to implement this if you want to use this with an AWS account
        throw new NotImplementedException();
    }
}
