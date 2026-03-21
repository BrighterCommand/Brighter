using System.Data;
using System.Data.Common;
using FluentMigrator.Runner;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Retry;

namespace DbMaker;

public static class SchemaCreation
{
    public static IHost CheckDbIsUp(this IHost webHost, ApplicationType applicationType)
    {
        using IServiceScope scope = webHost.Services.CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IConfiguration? config = services.GetService<IConfiguration>();
        if (config == null)
            throw new InvalidOperationException("Could not resolve IConfiguration");
        (Rdbms dbType, string? connectionString) = ConnectionResolver.ServerConnectionString(config, applicationType);
        if (connectionString == null)
            throw new InvalidOperationException("Could not resolve connection string; did you set a DbType?");

        WaitToConnect(dbType, connectionString);
        CreateDatabaseIfNotExists(dbType, DbConnectionFactory.GetConnection(dbType, connectionString));

        return webHost;
    }

    public static IHost MigrateDatabase(this IHost webHost)
    {
        using IServiceScope scope = webHost.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        try
        {
            IMigrationRunner runner = services.GetRequiredService<IMigrationRunner>();
            runner.ListMigrations();
            runner.MigrateUp();
        }
        catch (Exception ex)
        {
            ILogger<IHost> logger = services.GetRequiredService<ILogger<IHost>>();
            logger.LogError(ex, "An error occurred while migrating the database");
            throw;
        }

        return webHost;
    }

    private static void CreateDatabaseIfNotExists(Rdbms rdbms, DbConnection conn)
    {
        CreateGreetingsIfNotExists(rdbms, conn);
        CreateSalutationsIfNotExists(rdbms, conn);
    }

    private static void CreateGreetingsIfNotExists(Rdbms rdbms, DbConnection conn)
    {
        //don't use DDL for SQlite
        if (rdbms == Rdbms.Sqlite)
            return;

        //The migration does not create the Db, so we need to create it sot that it will add it
        conn.Open();
        using DbCommand command = conn.CreateCommand();

        command.CommandText = rdbms switch
        {
            Rdbms.MySql => "CREATE DATABASE IF NOT EXISTS Greetings",
            Rdbms.Postgres => "CREATE DATABASE Greetings",
            Rdbms.MsSql =>
                "IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'Greetings') CREATE DATABASE Greetings",
            _ => throw new InvalidOperationException("Could not create instance of Greetings for unknown Db type")
        };

        try
        {
            command.ExecuteScalar();
        }
        catch (NpgsqlException pe)
        {
            //Ignore if the Db already exists - we can't test for this in the SQL for Postgres
            if (!pe.Message.Contains("already exists"))
                throw;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Issue with creating Greetings tables, {e.Message}");
            //Rethrow, if we can't create the Outbox, shut down
            throw;
        }
    }

    private static void CreateSalutationsIfNotExists(Rdbms rdbms, DbConnection conn)
    {
        //don't use DDL for SQlite
        if (rdbms == Rdbms.Sqlite)
            return;

        //The migration does not create the Db, so we need to create it sot that it will add it
        if (conn.State != ConnectionState.Open)conn.Open();
        using DbCommand command = conn.CreateCommand();

        command.CommandText = rdbms switch
        {
            Rdbms.MySql => "CREATE DATABASE IF NOT EXISTS Salutations",
            Rdbms.Postgres => "CREATE DATABASE Salutations",
            Rdbms.MsSql =>
                "IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'Salutations') CREATE DATABASE Salutations",
            _ => throw new InvalidOperationException("Could not create instance of Salutations for unknown Db type")
        };

        try
        {
            command.ExecuteScalar();
        }
        catch (NpgsqlException pe)
        {
            //Ignore if the Db already exists - we can't test for this in the SQL for Postgres
            if (!pe.Message.Contains("already exists"))
                throw;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Issue with creating Greetings tables, {e.Message}");
            //Rethrow, if we can't create the Outbox, shut down
            throw;
        }
    }

    private static void WaitToConnect(Rdbms db, string connectionString)
    {
        RetryPolicy? policy = Policy.Handle<DbException>().WaitAndRetryForever(
            _ => TimeSpan.FromSeconds(2),
            (exception, _) =>
            {
                Console.WriteLine(
                    $"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
            });

        policy.Execute(() =>
        {
            using DbConnection conn = DbConnectionFactory.GetConnection(db, connectionString);
            conn.Open();
        });
    }
}
