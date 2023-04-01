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
using Paramore.Brighter.Outbox.MySql;
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
            string connectionString = DbServerConnectionString(config, env);

            //We don't check in development as using Sqlite
            if (env.IsDevelopment()) return webHost;

            WaitToConnect(connectionString);
            CreateDatabaseIfNotExists(GetDbConnection(GetDatabaseType(config), connectionString));

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

        public static IHost CreateOutbox(this IHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IWebHostEnvironment>();
                var config = services.GetService<IConfiguration>();

                CreateOutbox(config, env);
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


        private static void CreateOutbox(IConfiguration config, IWebHostEnvironment env)
        {
            try
            {
                var connectionString = DbConnectionString(config, env);

                if (env.IsDevelopment())
                    CreateOutboxDevelopment(connectionString);
                else
                    CreateOutboxProduction(GetDatabaseType(config), connectionString);
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Issue with creating Outbox table, {e.Message}");
                //Rethrow, if we can't create the Outbox, shut down
                throw;
            }
        }

        private static void CreateOutboxDevelopment(string connectionString)
        {
            CreateOutboxSqlite(connectionString);
        }

        private static void CreateOutboxSqlite(string connectionString)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = SqliteOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            using var reader = exists.ExecuteReader(CommandBehavior.SingleRow);

            if (reader.HasRows) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqliteOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME);
            command.ExecuteScalar();
        }

        private static void CreateOutboxProduction(DatabaseType databaseType, string connectionString)
        {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    CreateOutboxMySql(connectionString);
                    break;
                default:
                    throw new InvalidOperationException("Could not create instance of Outbox for unknown Db type");
            }
        }

        private static void CreateOutboxMySql(string connectionString)
        {
            using var sqlConnection = new MySqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = MySqlOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            bool exists = existsQuery.ExecuteScalar() != null;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = MySqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME);
            command.ExecuteScalar();
        }

        private static string DbConnectionString(IConfiguration config, IWebHostEnvironment env)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return env.IsDevelopment() ? GetDevDbConnectionString() : GetProductionDbConnectionString(config, GetDatabaseType(config)); 
        }

        private static string GetDevDbConnectionString()
        {
            return "Filename=Greetings.db;Cache=Shared";
        }

        private static string DbServerConnectionString(IConfiguration config, IWebHostEnvironment env)
        {
            return env.IsDevelopment() ? GetDevConnectionString() : GetProductionConnectionString(config, GetDatabaseType(config));
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
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }

        private static string GetProductionConnectionString(IConfiguration config, DatabaseType databaseType)
        {
            return databaseType switch
            {
                DatabaseType.MySql => config.GetConnectionString("GreetingsMySql"),
                _ => throw new InvalidOperationException("Could not determine the database type")
            };
        }

        private static string GetProductionDbConnectionString(IConfiguration config, DatabaseType databaseType)
        {
            return databaseType switch
            {
                DatabaseType.MySql => config.GetConnectionString("GreetingsMySql"),
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

        private static void WaitToConnect(string connectionString)
        {
            var policy = Policy.Handle<MySqlException>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(2),
                (exception, timespan) =>
                {
                    Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
                });

            policy.Execute(() =>
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
            });
        }
    }
}
