using Microsoft.Extensions.Hosting;

namespace SalutationAnalytics.Database
{
    public static class SchemaCreation
    {
        internal const string INBOX_TABLE_NAME = "Inbox";
        internal const string OUTBOX_TABLE_NAME = "Outbox";

        public static IHost CheckDbIsUp(this IHost host)
        {
            /*
            using var scope = host.Services.CreateScope();

            var services = scope.ServiceProvider;
            var env = services.GetService<IHostEnvironment>();
            var config = services.GetService<IConfiguration>();
            string connectionString = DbServerConnectionString(config, env);

            //We don't check in development as using Sqlite
            if (env.IsDevelopment()) return host;

            WaitToConnect(connectionString);
            CreateDatabaseIfNotExists(connectionString);
            
            */

            return host;
        }

        public static IHost CreateInbox(this IHost host)
        {
            /*
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var env = services.GetService<IHostEnvironment>();
                var config = services.GetService<IConfiguration>();

            }
            
            */

            return host;
        }

        public static IHost MigrateDatabase(this IHost host)
        {
            /*
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
            */

            return host;
        }
   }
}
