using System.Data;
using System.Data.Common;
using FluentMigrator.Runner;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.Sqlite;
using Polly;
using Polly.Retry;

namespace DbMaker;

public static class SchemaCreation
{
    private const string GREETINGS_DB = "Greetings";
    private const string SALUTATIONS_DB = "Salutations";
    private const string INBOX_TABLE_NAME = "Inbox";
    private const string OUTBOX_TABLE_NAME = "Outbox";

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

    public static IHost CreateInbox(this IHost host, string tableSchema)
    {
        using IServiceScope scope = host.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IConfiguration? config = services.GetService<IConfiguration>();
        if (config == null)
            throw new InvalidOperationException("Could not resolve IConfiguration");

        CreateInbox(tableSchema, config);

        return host;
    }

    public static IHost CreateOutbox(this IHost webHost, ApplicationType applicationType, string tableSchema, bool hasBinaryPayload)
    {
        using IServiceScope scope = webHost.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IConfiguration? config = services.GetService<IConfiguration>();
        if (config == null)
            throw new InvalidOperationException("Could not resolve IConfiguration");

        CreateOutbox(tableSchema, config, hasBinaryPayload, applicationType);

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

    private static void CreateInbox(string tableSchema, IConfiguration config)
    {
        string? dbType = config[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (dbType == null)
            throw new InvalidOperationException("Could not resolve DbType; did you set it in the environment?");

        string? connectionString =
            ConnectionResolver.DbConnectionString(config, ApplicationType.Salutations);
        if (connectionString == null)
            throw new InvalidOperationException(
                "Could not resolve connection string; did you set a connection string?");

        try
        {
            CreateInboxProduction(DbResolver.GetDatabaseType(dbType), tableSchema, connectionString);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Issue with creating Inbox table, {e.Message}");
            throw;
        }
    }

    private static void CreateInboxProduction(Rdbms rdbms, string tableSchema, string connectionString)
    {
        switch (rdbms)
        {
            case Rdbms.MySql:
                CreateInboxMySql(tableSchema, connectionString);
                break;
            case Rdbms.MsSql:
                CreateInboxMsSql(tableSchema, connectionString);
                break;
            case Rdbms.Postgres:
                CreateInboxPostgres(tableSchema, connectionString);
                break;
            case Rdbms.Sqlite:
                CreateInboxSqlite(connectionString);
                break;
            default:
                throw new InvalidOperationException("Could not create instance of Outbox for unknown Db type");
        }
    }

    private static void CreateInboxSqlite(string connectionString)
    {
        using SqliteConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using SqliteCommand exists = sqlConnection.CreateCommand();
        exists.CommandText = SqliteInboxBuilder.GetExistsQuery(INBOX_TABLE_NAME);
        using SqliteDataReader reader = exists.ExecuteReader(CommandBehavior.SingleRow);

        if (reader.HasRows) return;

        using SqliteCommand command = sqlConnection.CreateCommand();
        command.CommandText = SqliteInboxBuilder.GetDDL(INBOX_TABLE_NAME);
        command.ExecuteScalar();
    }

    private static void CreateInboxMySql(string tableSchema, string connectionString)
    {
        using MySqlConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using MySqlCommand existsQuery = sqlConnection.CreateCommand();
        existsQuery.CommandText = MySqlInboxBuilder.GetExistsQuery(tableSchema, INBOX_TABLE_NAME);
        object? findInbox = existsQuery.ExecuteScalar();
        bool exists = findInbox is long and > 0;

        if (exists) return;

        using MySqlCommand command = sqlConnection.CreateCommand();
        command.CommandText = MySqlInboxBuilder.GetDDL(INBOX_TABLE_NAME);
        command.ExecuteScalar();
    }

    private static void CreateInboxMsSql(string tableSchema, string connectionString)
    {
        using SqlConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using SqlCommand existsQuery = sqlConnection.CreateCommand();
        existsQuery.CommandText = SqlInboxBuilder.GetExistsQuery(tableSchema, INBOX_TABLE_NAME);
        object? findInbox = existsQuery.ExecuteScalar();
        bool exists = findInbox is > 0;

        if (exists) return;

        using SqlCommand command = sqlConnection.CreateCommand();
        command.CommandText = SqlInboxBuilder.GetDDL(INBOX_TABLE_NAME);
        command.ExecuteScalar();
    }

    private static void CreateInboxPostgres(string tableSchema, string connectionString)
    {
        using NpgsqlConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using NpgsqlCommand existsQuery = sqlConnection.CreateCommand();
        existsQuery.CommandText = PostgreSqlInboxBuilder.GetExistsQuery(tableSchema, INBOX_TABLE_NAME.ToLower());

        object? findInbox = existsQuery.ExecuteScalar();
        bool exists = findInbox is true;

        if (exists) return;


        using NpgsqlCommand command = sqlConnection.CreateCommand();
        command.CommandText = PostgreSqlInboxBuilder.GetDDL(INBOX_TABLE_NAME);
        command.ExecuteScalar();
    }

    private static void CreateOutbox(string tableSchema, IConfiguration config, bool hasBinaryPayload, ApplicationType applicationType)
    {
        string? dbType = config[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (dbType == null)
            throw new InvalidOperationException("Could not resolve DbType; did you set it in the environment?");

        try
        {
            (Rdbms databaseType, string? serverConnectionString) connectionString =
                ConnectionResolver.ServerConnectionString(config, applicationType);
            if (connectionString.serverConnectionString == null)
                throw new InvalidOperationException(
                    "Could not resolve connection string; did you set a server connection string?");
            string? dbConnectionString = ConnectionResolver.DbConnectionString(config, applicationType);
            if (dbConnectionString == null)
                throw new InvalidOperationException(
                    "Could not resolve connection string; did you set a db connection string?");

            CreateOutboxProduction(
                DbResolver.GetDatabaseType(dbType),
                (connectionString.databaseType, tableSchema, dbConnectionString),
                hasBinaryPayload
            );
        }
        catch (NpgsqlException pe)
        {
            //Ignore if the Db already exists - we can't test for this in the SQL for Postgres
            if (!pe.Message.Contains("already exists"))
            {
                Console.WriteLine($"Issue with creating Outbox table, {pe.Message}");
                throw;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Issue with creating Outbox table, {e.Message}");
            //Rethrow, if we can't create the Outbox, shut down
            throw;
        }
    }

    private static void CreateOutboxProduction(
        Rdbms rdbms,
        (Rdbms databaseType, string tableSchema, string? serverConnectionString) db,
        bool hasBinaryPayload
    )
    {
        switch (rdbms)
        {
            case Rdbms.MySql:
                CreateOutboxMySql(db.tableSchema, db.serverConnectionString, hasBinaryPayload);
                break;
            case Rdbms.MsSql:
                CreateOutboxMsSql(db.tableSchema, db.serverConnectionString, hasBinaryPayload);
                break;
            case Rdbms.Postgres:
                CreateOutboxPostgres(db.tableSchema, db.serverConnectionString, hasBinaryPayload);
                break;
            case Rdbms.Sqlite:
                CreateOutboxSqlite(db.serverConnectionString, hasBinaryPayload);
                break;
            default:
                throw new InvalidOperationException("Could not create instance of Outbox for unknown Db type");
        }
    }

    private static void CreateOutboxMsSql(string tableSchema, string? connectionString, bool hasBinaryPayload)
    {
        using SqlConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using SqlCommand? existsQuery = sqlConnection.CreateCommand();
        existsQuery.CommandText = SqlOutboxBuilder.GetExistsQuery(tableSchema, OUTBOX_TABLE_NAME);
        object? findOutbox = existsQuery.ExecuteScalar();
        bool exists = findOutbox is > 0;

        if (exists) return;

        using SqlCommand? command = sqlConnection.CreateCommand();
        command.CommandText = SqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
        command.ExecuteScalar();
    }

    private static void CreateOutboxMySql(string tableSchema, string? connectionString, bool hasBinaryPayload)
    {
        using MySqlConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using MySqlCommand existsQuery = sqlConnection.CreateCommand();
        existsQuery.CommandText = MySqlOutboxBuilder.GetExistsQuery(tableSchema, OUTBOX_TABLE_NAME);
        object? findOutbox = existsQuery.ExecuteScalar();
        bool exists = findOutbox is long and > 0;

        if (exists) return;

        using MySqlCommand command = sqlConnection.CreateCommand();
        command.CommandText = MySqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
        command.ExecuteScalar();
    }

    private static void CreateOutboxPostgres(string tableSchema, string? connectionString, bool hasBinaryPayload)
    {
        using NpgsqlConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using NpgsqlCommand existsQuery = sqlConnection.CreateCommand();
        existsQuery.CommandText = PostgreSqlOutboxBuilder.GetExistsQuery(tableSchema.ToLower(), OUTBOX_TABLE_NAME.ToLower());
        object? findOutbox = existsQuery.ExecuteScalar();
        bool exists = (bool)findOutbox; 

        if (exists) return;

        using NpgsqlCommand command = sqlConnection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME.ToLower(), hasBinaryPayload);
        command.ExecuteScalar();
    }

    private static void CreateOutboxSqlite(string? connectionString, bool hasBinaryPayload)
    {
        using SqliteConnection sqlConnection = new(connectionString);
        sqlConnection.Open();

        using SqliteCommand exists = sqlConnection.CreateCommand();
        exists.CommandText = SqliteOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
        using SqliteDataReader reader = exists.ExecuteReader(CommandBehavior.SingleRow);

        if (reader.HasRows) return;

        using SqliteCommand command = sqlConnection.CreateCommand();
        command.CommandText = SqliteOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
        command.ExecuteScalar();
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
