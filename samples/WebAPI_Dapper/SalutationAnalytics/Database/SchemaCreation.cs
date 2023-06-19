using System;
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

namespace SalutationAnalytics.Database
{
    public static class SchemaCreation
    {
        internal const string INBOX_TABLE_NAME = "Inbox";
        internal const string OUTBOX_TABLE_NAME = "Outbox";

        public static IHost CheckDbIsUp(this IHost host)
        {
            using var scope = host.Services.CreateScope();

            var services = scope.ServiceProvider;
            var env = services.GetService<IHostEnvironment>();
            var config = services.GetService<IConfiguration>();
            var (dbType, connectionString) = DbServerConnectionString(config, env);

            //We don't check db availability in development as we always use Sqlite which is a file not a server
            if (env.IsDevelopment()) return host;

            WaitToConnect(dbType, connectionString);
            CreateDatabaseIfNotExists(dbType, GetDbConnection(dbType, connectionString));

            return host;
        }

        public static IHost CreateInbox(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IHostEnvironment>();
                var config = services.GetService<IConfiguration>();

                CreateInbox(config, env);
            }

            return host;
        }
        
        public static IHost CreateOutbox(this IHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IHostEnvironment>();
                var config = services.GetService<IConfiguration>();

                CreateOutbox(config, env);
            }

            return webHost;
        }

        public static IHost MigrateDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
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

            return host;
        }

        private static void CreateDatabaseIfNotExists(DatabaseType databaseType, DbConnection conn)
        {
            //The migration does not create the Db, so we need to create it sot that it will add it
            conn.Open();
            using var command = conn.CreateCommand();

            command.CommandText = databaseType switch
            {
                DatabaseType.Sqlite => "CREATE DATABASE IF NOT EXISTS Salutations",
                DatabaseType.MySql => "CREATE DATABASE IF NOT EXISTS Salutations",
                DatabaseType.Postgres => "CREATE DATABASE Salutations",
                DatabaseType.MsSql =>
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
            catch (System.Exception e)
            {
                Console.WriteLine($"Issue with creating Greetings tables, {e.Message}");
                //Rethrow, if we can't create the Outbox, shut down
                throw;
            }
        }
        private static void CreateInbox(IConfiguration config, IHostEnvironment env)
        {
            try
            {
                var connectionString = DbConnectionString(config, env);

                if (env.IsDevelopment())
                    CreateInboxDevelopment(connectionString);
                else
                    CreateInboxProduction(GetDatabaseType(config), connectionString);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Issue with creating Inbox table, {e.Message}");
                throw;
            }
        }

        private static void CreateInboxDevelopment(string connectionString)
        {
            CreateInboxSqlite(connectionString);
        }

        private static void CreateInboxProduction(DatabaseType databaseType, string connectionString)
        {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    CreateInboxMySql(connectionString);
                    break;
                case DatabaseType.MsSql:
                    CreateInboxMsSql(connectionString);
                    break;
                case DatabaseType.Postgres:
                    CreateInboxPostgres(connectionString);
                    break;
                case DatabaseType.Sqlite:
                    CreateInboxSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException("Could not create instance of Outbox for unknown Db type");
            }
        }

        private static void CreateInboxSqlite(string connectionString)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = SqliteInboxBuilder.GetExistsQuery(INBOX_TABLE_NAME);
            using var reader = exists.ExecuteReader(CommandBehavior.SingleRow);

            if (reader.HasRows) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqliteInboxBuilder.GetDDL(INBOX_TABLE_NAME);
            command.ExecuteScalar();
        }

        private static void CreateInboxMySql(string connectionString)
        {
            using var sqlConnection = new MySqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = MySqlInboxBuilder.GetExistsQuery(INBOX_TABLE_NAME);
            bool exists = existsQuery.ExecuteScalar() != null;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = MySqlInboxBuilder.GetDDL(INBOX_TABLE_NAME);
            command.ExecuteScalar();
        }
        
        private static void CreateInboxMsSql(string connectionString)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = SqlInboxBuilder.GetExistsQuery(INBOX_TABLE_NAME);
            using var reader = exists.ExecuteReader(CommandBehavior.SingleRow);

            if (reader.HasRows) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqlInboxBuilder.GetDDL(INBOX_TABLE_NAME);
            command.ExecuteScalar();
        }
        
        private static void CreateInboxPostgres(string connectionString)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = PostgreSqlInboxBuilder.GetExistsQuery(INBOX_TABLE_NAME);
            using var reader = exists.ExecuteReader(CommandBehavior.SingleRow);

            if (reader.HasRows) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = PostgreSqlInboxBuilder.GetDDL(INBOX_TABLE_NAME);
            command.ExecuteScalar();
        }

        private static void CreateOutbox(IConfiguration config, IHostEnvironment env)
        {
            try
            {
                var connectionString = DbConnectionString(config, env);

                if (env.IsDevelopment())
                    CreateOutboxDevelopment(connectionString);
                else
                    CreateOutboxProduction(GetDatabaseType(config), connectionString);
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

       private static void CreateOutboxProduction(DatabaseType databaseType, string connectionString) 
       {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    CreateOutboxMySql(connectionString);
                    break;
                case DatabaseType.MsSql:
                    CreateOutboxMsSql(connectionString);
                    break;
                case DatabaseType.Postgres:
                    CreateOutboxPostgres(connectionString);
                    break;
                case DatabaseType.Sqlite:
                    CreateOutboxSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException("Could not create instance of Outbox for unknown Db type");
            }
        }

       private static void CreateOutboxMsSql(string connectionString)
       {
            using var sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = SqlOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            bool exists = existsQuery.ExecuteScalar() != null;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME);
            command.ExecuteScalar();
            
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
        
        private static void CreateOutboxPostgres(string connectionString)
        {
             using var sqlConnection = new NpgsqlConnection(connectionString);
             sqlConnection.Open();
 
             using var existsQuery = sqlConnection.CreateCommand();
             existsQuery.CommandText = PostgreSqlOutboxBulder.GetExistsQuery(OUTBOX_TABLE_NAME);
             bool exists = existsQuery.ExecuteScalar() != null;
 
             if (exists) return;
 
             using var command = sqlConnection.CreateCommand();
             command.CommandText = PostgreSqlOutboxBulder.GetDDL(OUTBOX_TABLE_NAME);
             command.ExecuteScalar();
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

        private static string DbConnectionString(IConfiguration config, IHostEnvironment env)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return env.IsDevelopment() ? GetDevConnectionString() : GetProductionDbConnectionString(config, GetDatabaseType(config)); 
        }

        private static (DatabaseType, string) DbServerConnectionString(IConfiguration config, IHostEnvironment env)
        {
            var databaseType = GetDatabaseType(config);
            var connectionString = env.IsDevelopment() ? GetDevConnectionString() : GetProductionConnectionString(config, databaseType);
            return (databaseType, connectionString);
        }

        private static string GetDevConnectionString()
        {
            return "Filename=Salutations.db;Cache=Shared";
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
