using System;
using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace GreetingsWeb.Database
{
    public static class SchemaCreation
    {
        private const string OUTBOX_TABLE_NAME = "Outbox";

        public static IHost CheckDbIsUp(this IHost webHost)
        {
            /*
            using var scope = webHost.Services.CreateScope();

            var services = scope.ServiceProvider;
            var env = services.GetService<IWebHostEnvironment>();
            var config = services.GetService<IConfiguration>();
            string connectionString = DbServerConnectionString(config, env);

            //We don't check in development as using Sqlite
            if (env.IsDevelopment()) return webHost;

            WaitToConnect(connectionString);
            CreateDatabaseIfNotExists(GetDbConnection(GetDatabaseType(config), connectionString));
            */

            return webHost;
        }

        public static IHost MigrateDatabase(this IHost webHost)
        {
            /*
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
            */

            return webHost;
        }

        public static IHost CreateOutbox(this IHost webHost)
        {
            /*
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IWebHostEnvironment>();
                var config = services.GetService<IConfiguration>();

                CreateOutbox(config, env);
            }
            */

            return webHost;
        }
   }
}
