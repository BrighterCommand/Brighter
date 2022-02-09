using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Polly;
using SalutationPorts.EntityGateway;

namespace SalutationAnalytics.Database
{
    public static class SchemaCreation
    {
        private const string INBOX_TABLE_NAME = "Inbox";
        private const string OUTBOX_TABLE_NAME = "Outbox";


        public static IHost MigrateDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                try
                {
                    var db = services.GetRequiredService<SalutationsEntityGateway>();

                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }

            return host;
        }

        public static IHost CheckDbIsUp(this IHost host)
        {
            using var scope = host.Services.CreateScope() ;
            
            var services = scope.ServiceProvider;
            var env = services.GetService<IHostEnvironment>();
            var config = services.GetService<IConfiguration>();
            string connectionString = DbServerConnectionString(config, env);

            //We don't check in development as using Sqlite
            if (env.IsDevelopment()) return host;

            WaitToConnect(connectionString);
            CreateDatabaseIfNotExists(connectionString);

            return host;
        }

        private static void CreateDatabaseIfNotExists(string connectionString)
        {
            //The migration does not create the Db, so we need to create it sot that it will add it
            using var conn = new MySqlConnection(connectionString);
            conn.Open();
            using var command = conn.CreateCommand();
            command.CommandText = "CREATE DATABASE IF NOT EXISTS Salutations";
            command.ExecuteScalar();
        }

        private static void WaitToConnect(string connectionString)
        {
            var policy = Policy.Handle<MySqlException>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(2),
                (exception, timespan) => { Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}"); });

            policy.Execute(() =>
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
            });
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

        private static void CreateInbox(IConfiguration config, IHostEnvironment env)
        {
            try
            {
                var connectionString = DbConnectionString(config, env);

                if (env.IsDevelopment())
                    CreateInboxDevelopment(connectionString);
                else
                    CreateInboxProduction(connectionString);
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Issue with creating Inbox table, {e.Message}");
                throw;
            }
        }

        private static void CreateInboxDevelopment(string connectionString)
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

        private static void CreateInboxProduction(string connectionString)
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

        private static void CreateOutbox(IConfiguration config, IHostEnvironment env)
        {
            try
            {
                var connectionString = DbConnectionString(config, env);

                if (env.IsDevelopment())
                    CreateOutboxDevelopment(connectionString);
                else
                    CreateOutboxProduction(connectionString);
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Issue with creating Outbox table, {e.Message}");
                throw;
            }
        }

        private static void CreateOutboxDevelopment(string connectionString)
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

        private static void CreateOutboxProduction(string connectionString)
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

        private static string DbConnectionString(IConfiguration config, IHostEnvironment env)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return env.IsDevelopment() ? "Filename=Salutations.db;Cache=Shared" : config.GetConnectionString("Salutations");
        }

        private static string DbServerConnectionString(IConfiguration config, IHostEnvironment env)
        {
            return env.IsDevelopment() ? "Filename=Salutations.db;Cache=Shared" : config.GetConnectionString("SalutationsDb");
         }
    }
}
