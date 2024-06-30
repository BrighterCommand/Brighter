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
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.Sqlite;
using Polly;

namespace GreetingsDb
{
    public static class SchemaCreation
    {
        private const string OUTBOX_TABLE_NAME = "Outbox";

        public static IHost CheckDbIsUp(this IHost webHost)
        {
            using var scope = ServiceProviderServiceExtensions.CreateScope(webHost.Services);

            var services = scope.ServiceProvider;
            var config = services.GetService<IConfiguration>();
            var (dbType, connectionString) = ConnectionResolver.ServerConnectionString(config);

            WaitToConnect(dbType, connectionString);
            CreateDatabaseIfNotExists(dbType, DbConnectionFactory.GetConnection(dbType, connectionString));

            return webHost;
        }
        
        public static IHost CreateOutbox(this IHost webHost, bool hasBinaryPayload)
        {
            using var scope = ServiceProviderServiceExtensions.CreateScope(webHost.Services);
            var services = scope.ServiceProvider;
            var config = services.GetService<IConfiguration>();

            CreateOutbox(config, hasBinaryPayload);

            return webHost;
        }



        public static IHost MigrateDatabase(this IHost webHost)
        {
            using var scope = ServiceProviderServiceExtensions.CreateScope(webHost.Services);
            var services = scope.ServiceProvider;

            try
            {
                var runner = services.GetRequiredService<IMigrationRunner>();
                runner.ListMigrations();
                runner.MigrateUp();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<IHost>>();
                LoggerExtensions.LogError(logger, ex, "An error occurred while migrating the database.");
                throw;
            }

            return webHost;
        }

        private static void CreateDatabaseIfNotExists(DatabaseType databaseType, DbConnection conn)
        {
            //The migration does not create the Db, so we need to create it sot that it will add it
            conn.Open();
            using var command = conn.CreateCommand();

            command.CommandText = databaseType switch
            {
                DatabaseType.Sqlite => "CREATE DATABASE IF NOT EXISTS Greetings",
                DatabaseType.MySql => "CREATE DATABASE IF NOT EXISTS Greetings",
                DatabaseType.Postgres => "CREATE DATABASE Greetings",
                DatabaseType.MsSql =>
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

        private static void CreateOutbox(IConfiguration config, bool hasBinaryPayload)
        {
            try
            {
                var connectionString = ConnectionResolver.DbConnectionString(config);

                CreateOutboxProduction(
                        DbResolver.GetDatabaseType(config[DatabaseGlobals.DATABASE_TYPE_ENV]), 
                        connectionString, 
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

       private static void CreateOutboxProduction(DatabaseType databaseType, string? connectionString, bool hasBinaryPayload) 
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

       private static void CreateOutboxMsSql(string? connectionString, bool hasBinaryPayload)
       {
            using var sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = SqlOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            var findOutbox = existsQuery.ExecuteScalar();
            bool exists = findOutbox is > 0;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = SqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
            command.ExecuteScalar();
        }

        private static void CreateOutboxMySql(string? connectionString, bool hasBinaryPayload)
        {
            using var sqlConnection = new MySqlConnection(connectionString);
            sqlConnection.Open();

            using var existsQuery = sqlConnection.CreateCommand();
            existsQuery.CommandText = MySqlOutboxBuilder.GetExistsQuery(OUTBOX_TABLE_NAME);
            var findOutbox = existsQuery.ExecuteScalar();
            bool exists = findOutbox is long and > 0;

            if (exists) return;

            using var command = sqlConnection.CreateCommand();
            command.CommandText = MySqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
            command.ExecuteScalar();
        }
        
        private static void CreateOutboxPostgres(string? connectionString, bool hasBinaryPayload)
        {
             using var sqlConnection = new NpgsqlConnection(connectionString);
             sqlConnection.Open();
 
             using var existsQuery = sqlConnection.CreateCommand();
             existsQuery.CommandText = PostgreSqlOutboxBulder.GetExistsQuery(OUTBOX_TABLE_NAME);
             var findOutbox = existsQuery.ExecuteScalar();
             bool exists = findOutbox is long and > 0;

             if (exists) return;
 
             using var command = sqlConnection.CreateCommand();
             command.CommandText = PostgreSqlOutboxBulder.GetDDL(OUTBOX_TABLE_NAME, hasBinaryPayload);
             command.ExecuteScalar();
        }

        private static void CreateOutboxSqlite(string? connectionString, bool hasBinaryPayload)
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

        private static void WaitToConnect(DatabaseType dbType, string connectionString)
        {
            var policy = Policy.Handle<DbException>().WaitAndRetryForever(
                _ => TimeSpan.FromSeconds(2),
                (exception, _) =>
                {
                    Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
                });

            policy.Execute(() =>
            {
                using var conn = DbConnectionFactory.GetConnection(dbType, connectionString);
                conn.Open();
            });
        }
    }
}
