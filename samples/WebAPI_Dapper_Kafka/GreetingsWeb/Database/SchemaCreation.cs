using System;
using System.Data;
using System.Data.Common;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.Sqlite;
using Polly;

namespace GreetingsWeb.Database
{
    public static class SchemaCreation
    {
        private const string OUTBOX_TABLE_NAME = "Outbox";

        public static IHost CheckDbIsUp(this IHost webHost)
        {
            using var scope = webHost.Services.CreateScope();

            var services = scope.ServiceProvider;
            var env = services.GetService<IWebHostEnvironment>();
            var config = services.GetService<IConfiguration>();
            var (dbType, connectionString) = DbServerConnectionString(config, env);

            //We don't check db availability in development as we always use Sqlite which is a file not a server
            if (env.IsDevelopment()) return webHost;

            WaitToConnect(dbType, connectionString);
            CreateDatabaseIfNotExists(GetDbConnection(dbType, connectionString));

            return webHost;
        }

        public static IHost MigrateDatabase(this IHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                try
                {
                    var runner = services.GetRequiredService<IMigrationRunner>();
                    runner.ListMigrations();
                    runner.MigrateUp();
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                    throw;
                }
            }

            return webHost;
        }

        public static IHost CreateOutbox(this IHost webHost, bool hasBinaryPayload = false)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IWebHostEnvironment>();
                var config = services.GetService<IConfiguration>();

                CreateOutbox(config, env, hasBinaryPayload);
            }

            return webHost;
        }

        private static void CreateDatabaseIfNotExists(DbConnection conn)
        {
            //The migration does not create the Db, so we need to create it sot that it will add it
            conn.Open();
            using var command = conn.CreateCommand();
            command.CommandText = "CREATE DATABASE IF NOT EXISTS Greetings";
            command.ExecuteScalar();
        }


        private static void CreateOutbox(IConfiguration config, IWebHostEnvironment env, bool hasBinaryPayload)
        {
            try
            {
                var connectionString = DbConnectionString(config, env);

                if (env.IsDevelopment())
                    CreateOutboxDevelopment(connectionString, hasBinaryPayload);
                else
                    CreateOutboxProduction(GetDatabaseType(config), connectionString, hasBinaryPayload);
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Issue with creating Outbox table, {e.Message}");
                //Rethrow, if we can't create the Outbox, shut down
                throw;
            }
        }

        private static void CreateOutboxDevelopment(string connectionString, bool hasBinaryPayload)
        {
            CreateOutboxSqlite(connectionString, hasBinaryPayload);
        }

       private static void CreateOutboxProduction(DatabaseType databaseType, string connectionString,
           bool hasBinaryPayload) 
       {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    CreateOutboxMySql(connectionString, hasBinaryPayload);
                    break;
                case DatabaseType.MsSql:
                    CreateOutboxMsSql(connectionString, hasBinaryPayload);
                    break;
                case DatabaseType.Postgres:
                    CreateOutboxPostgres(connectionString, hasBinaryPayload);
                    break;
                case DatabaseType.Sqlite:
                    CreateOutboxSqlite(connectionString, hasBinaryPayload);
                    break;
                default:
                    throw new InvalidOperationException("Could not create instance of Outbox for unknown Db type");
            }
        }

       private static void CreateOutboxMsSql(string connectionString, bool hasBinaryPayload)
       {
            using var sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = SqlOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            bool exists = existsQuery.ExecuteScalar() != null;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
            command.ExecuteScalar();
            
        }

        private static void CreateOutboxMySql(string connectionString, bool hasBinaryPayload)
        {
            using var sqlConnection = new MySqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = MySqlOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            bool exists = existsQuery.ExecuteScalar() != null;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = MySqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
            command.ExecuteScalar();
        }
        
        private static void CreateOutboxPostgres(string connectionString, bool hasBinaryPayload)
        {
             using var sqlConnection = new NpgsqlConnection(connectionString);
             sqlConnection.Open();
 
             using var existsQuery = sqlConnection.CreateCommand();
             existsQuery.CommandText = PostgreSqlOutboxBulder.GetExistsQuery(OUTBOX_TABLE_NAME);
             bool exists = existsQuery.ExecuteScalar() != null;
 
             if (exists) return;
 
             using var command = sqlConnection.CreateCommand();
             command.CommandText = PostgreSqlOutboxBulder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
             command.ExecuteScalar();
        }

        private static void CreateOutboxSqlite(string connectionString, bool hasBinaryPayload)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = SqliteOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            using var reader = exists.ExecuteReader(CommandBehavior.SingleRow);

            if (reader.HasRows) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqliteOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
            command.ExecuteScalar();
        }

        private static string DbConnectionString(IConfiguration config, IWebHostEnvironment env)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return env.IsDevelopment() ? GetDevConnectionString() : GetProductionDbConnectionString(config, GetDatabaseType(config)); 
        }

        private static (DatabaseType, string) DbServerConnectionString(IConfiguration config, IWebHostEnvironment env)
        {
            var databaseType = GetDatabaseType(config);
            var connectionString = env.IsDevelopment() ? GetDevConnectionString() : GetProductionConnectionString(config, databaseType);
            return (databaseType, connectionString);
        }

        private static string GetDevConnectionString()
        {
            return "Filename=Greetings.db;Cache=Shared";
        }

        private static DbConnection GetDbConnection(DatabaseType databaseType, string connectionString)
        {
            return databaseType switch
            {
                DatabaseType.MySql => new MySqlConnection(connectionString),
                DatabaseType.MsSql => new SqlConnection(connectionString),
                DatabaseType.Postgres => new NpgsqlConnection(connectionString),
                DatabaseType.Sqlite => new SqliteConnection(connectionString),
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }
        
        private static string GetProductionConnectionString(IConfiguration config, DatabaseType databaseType)
        {
            return databaseType switch
            { 
                DatabaseType.MySql => config.GetConnectionString("MySqlDb"),
                DatabaseType.MsSql => config.GetConnectionString("MsSqlDb"),
                DatabaseType.Postgres => config.GetConnectionString("PostgreSqlDb"),
                DatabaseType.Sqlite => GetDevConnectionString(),
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }

        private static string GetProductionDbConnectionString(IConfiguration config, DatabaseType databaseType)
        {
            return databaseType switch
            {
                DatabaseType.MySql => config.GetConnectionString("GreetingsMySql"),
                DatabaseType.MsSql => config.GetConnectionString("GreetingsMsSql"),
                DatabaseType.Postgres => config.GetConnectionString("GreetingsPostgreSql"),
                DatabaseType.Sqlite => GetDevConnectionString(),
                _ => throw new InvalidOperationException("Could not determine the database type")
             };
        }

        private static DatabaseType GetDatabaseType(IConfiguration config)
        {
            return config[DatabaseGlobals.DATABASE_TYPE_ENV] switch
            {
                DatabaseGlobals.MYSQL => DatabaseType.MySql,
                DatabaseGlobals.MSSQL => DatabaseType.MsSql,
                DatabaseGlobals.POSTGRESSQL => DatabaseType.Postgres,
                DatabaseGlobals.SQLITE => DatabaseType.Sqlite,
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }

        private static void WaitToConnect(DatabaseType dbType, string connectionString)
        {
            var policy = Policy.Handle<DbException>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(2),
                (exception, timespan) =>
                {
                    Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
                });

            policy.Execute(() =>
            {
                using var conn = GetConnection(dbType, connectionString);
                conn.Open();
            });
        }

        private static DbConnection GetConnection(DatabaseType databaseType, string connectionString)
        {
            return databaseType switch
            {
                DatabaseType.MySql => new MySqlConnection(connectionString),
                DatabaseType.MsSql => new SqlConnection(connectionString),
                DatabaseType.Postgres => new NpgsqlConnection(connectionString),
                DatabaseType.Sqlite => new SqliteConnection(connectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null)
            };
        }
    }
}
