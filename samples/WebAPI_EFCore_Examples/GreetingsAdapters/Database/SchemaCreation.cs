using System;
using System.Data;
using GreetingsPorts.EntityGateway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Polly;

namespace GreetingsAdapters.Database
{
    public static class SchemaCreation
    {
        private const string OUTBOX_TABLE_NAME = "Outbox";

        public static IHost MigrateDatabase(this IHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                try
                {
                    var db = services.GetRequiredService<GreetingsEntityGateway>();

                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }

            return webHost;
        }

        public static IHost CheckDbIsUp(this IHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IWebHostEnvironment>();
                var config = services.GetService<IConfiguration>();
                string connectionString = DbConnectionString(config, env);

                var policy = Policy.Handle<MySqlException>().WaitAndRetryForever(
                    retryAttempt => TimeSpan.FromSeconds(2),
                    (exception, timespan) =>
                    {
                        Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
                    });

                policy.Execute(() =>
                {
                    //don't check this for SQlite in development
                    if (!env.IsDevelopment())
                    {
                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();
                        }
                    }
                });
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

        private static void CreateOutbox(IConfiguration config, IWebHostEnvironment env)
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
            }
        }

        private static void CreateOutboxDevelopment(string connectionString)
        {
            using var sqlConnection = new SqliteConnection(connectionString);
            sqlConnection.Open();

            using var exists = sqlConnection.CreateCommand();
            exists.CommandText = SqliteOutboxBuilder.GetExists(OUTBOX_TABLE_NAME);
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

            using var command = sqlConnection.CreateCommand();
            command.CommandText = MySqlOutboxBuilder.GetDDL(OUTBOX_TABLE_NAME);
            command.ExecuteScalar();
        }

        private static string DbConnectionString(IConfiguration config, IWebHostEnvironment env)
        {
            //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
            return env.IsDevelopment() ? "Filename=Greetings.db;Cache=Shared" : config.GetConnectionString("GreetingsDb");
        }
    }
}
