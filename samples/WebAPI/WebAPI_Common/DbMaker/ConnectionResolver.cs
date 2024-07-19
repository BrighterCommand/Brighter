﻿using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DbMaker;

public static class ConnectionResolver
{
    public static string? GreetingsDbConnectionString(IConfiguration configuration)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

        Rdbms rdbms = DbResolver.GetDatabaseType(dbType);
        return rdbms switch
        {
            Rdbms.MySql => configuration.GetConnectionString("GreetingsMySql"),
            Rdbms.MsSql => configuration.GetConnectionString("GreetingsMsSql"),
            Rdbms.Postgres => configuration.GetConnectionString("GreetingsPostgreSql"),
            Rdbms.Sqlite => configuration.GetConnectionString("GreetingsSqlite"), 
            _ => throw new InvalidOperationException("Could not determine the database type")
        };
    }

    public static string? GetSalutationsDbConnectionString(IConfiguration config, Rdbms rdbms)
    {
        return rdbms switch
        {
            Rdbms.MySql => config.GetConnectionString("SalutationsMySql"),
            Rdbms.MsSql => config.GetConnectionString("SalutationsMsSql"),
            Rdbms.Postgres => config.GetConnectionString("SalutationsPostgreSql"),
            Rdbms.Sqlite => config.GetConnectionString("SalutationsSqlite"),
            _ => throw new InvalidOperationException("Could not determine the database type")
        };
    }

    public static (Rdbms databaseType, string? connectionString) ServerConnectionString(
        IConfiguration configuration,
        ApplicationType applicationType)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

        Rdbms rdbms = DbResolver.GetDatabaseType(dbType);
        string? connectionString = rdbms switch
        {
            Rdbms.MySql => configuration.GetConnectionString("MySqlDb"),
            Rdbms.MsSql => configuration.GetConnectionString("MsSqlDb"),
            Rdbms.Postgres => configuration.GetConnectionString("PostgreSqlDb"),
            Rdbms.Sqlite => applicationType == ApplicationType.Greetings ? 
                configuration.GetConnectionString("GreetingsSqlite") : 
                configuration.GetConnectionString("SalutationsSqlite"), 
            _ => throw new InvalidOperationException("Could not determine the database type")
        };
        return (rdbms, connectionString);
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
