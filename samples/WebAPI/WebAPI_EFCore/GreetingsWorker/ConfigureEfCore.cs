using DbMaker;
using GreetingsApp.EntityGateway;
using Microsoft.EntityFrameworkCore;

namespace GreetingsWorker
{
    public static class EfCoreExtensions
    {
        public static void ConfigureEfCore<TContext>(this IServiceCollection services, IConfiguration configuration) where TContext : DbContext
        {
            string connectionString = ConnectionResolver.DbConnectionString(configuration, ApplicationType.Greetings);
            string configDbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];

            if (string.IsNullOrWhiteSpace(configDbType))
                throw new InvalidOperationException("DbType is not set");

            var dbType = DbResolver.GetDatabaseType(configDbType);

            switch (dbType)
            {
                case Rdbms.Sqlite:
                    ConfigureSqlite<TContext>(services, connectionString);
                    break;
                case Rdbms.MySql:
                    ConfigureMySql<TContext>(services, connectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Database type {dbType} is not supported");
            }
        }

        private static void ConfigureMySql<TContext>(IServiceCollection services, string connectionString) where TContext : DbContext
        {
            services.AddDbContextPool<GreetingsEntityGateway>(builder =>
            {
                builder
                    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
        }

        private static void ConfigureSqlite<TContext>(IServiceCollection services, string connectionString) where TContext : DbContext
        {
            services.AddDbContext<GreetingsEntityGateway>(
                builder =>
                {
                    builder.UseSqlite(connectionString)
                        .EnableDetailedErrors()
                        .EnableSensitiveDataLogging();
                });
        }
    }
}
