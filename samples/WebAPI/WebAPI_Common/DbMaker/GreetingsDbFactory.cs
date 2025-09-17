using FluentMigrator.Runner;
using GreetingsMigrations.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DbMaker;

public static class GreetingsDbFactory
{
    public static void ConfigureMigration(IConfiguration configuration, IServiceCollection services)
    {
        MakeGreetingsDatabase(configuration, services);
    }

    private static void MakeGreetingsDatabase(IConfiguration configuration, IServiceCollection services)
    {
        string? greetingsDbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(greetingsDbType))
            throw new InvalidOperationException("DbType is not set");

        Rdbms rdbms = DbResolver.GetDatabaseType(greetingsDbType);

        switch (rdbms)
        {
            case Rdbms.MySql:
                ConfigureMySql(configuration, services);
                break;
            case Rdbms.MsSql:
                ConfigureMsSql(configuration, services);
                break;
            case Rdbms.Postgres:
                ConfigurePostgreSql(configuration, services);
                break;
            case Rdbms.Sqlite:
                ConfigureSqlite(configuration, services);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rdbms), "Database type is not supported");
        }
    }

    private static void ConfigureMsSql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddSqlServer()
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Greetings))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = Rdbms.MsSql.ToString()
            });
    }

    private static void ConfigureMySql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddMySql5()
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Greetings))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = Rdbms.MySql.ToString()
            });
    }

    private static void ConfigurePostgreSql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddPostgres()
                .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Greetings))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = Rdbms.Postgres.ToString()
            });
    }

    private static void ConfigureSqlite(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddSQLite()
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Greetings))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = Rdbms.Sqlite.ToString()
            });
    }
}
