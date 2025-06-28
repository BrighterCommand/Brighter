using FluentMigrator.Runner;
using FluentMigrator.Runner.Processors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Salutations_Migrations.Migrations;

namespace DbMaker;

public class SalutationsDbFactory
{
    public static void ConfigureMigration(IConfiguration configuration, IServiceCollection services)
    {
        string? dbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(dbType))
            throw new InvalidOperationException("DbType is not set");

        BuildSalutationsDatabase(configuration, DbResolver.GetDatabaseType(dbType), services);
    }

    static void BuildSalutationsDatabase(
        IConfiguration configuration,
        Rdbms rdbms,
        IServiceCollection services)
    {
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

    static void ConfigureMySql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddMySql5()
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Salutations))
                .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
            )
            .Configure<SelectingProcessorAccessorOptions>(opt => opt.ProcessorId = "MySql5");
    }

    static void ConfigureMsSql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddSqlServer()
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Salutations))
                .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
            )
            .Configure<SelectingProcessorAccessorOptions>(opt => opt.ProcessorId = "SqlServer");
    }

    static void ConfigurePostgreSql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddPostgres()
                .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
                .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(configuration, ApplicationType.Salutations))
                .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
            )
            .Configure<SelectingProcessorAccessorOptions>(opt => opt.ProcessorId = "Postgres");
    }

    static void ConfigureSqlite(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c =>
            {
                c.AddSQLite()
                    .WithGlobalConnectionString(
                        ConnectionResolver.DbConnectionString(configuration, ApplicationType.Salutations))
                    .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations();
            })
            .Configure<SelectingProcessorAccessorOptions>(opt => opt.ProcessorId = "SQLite");
    }
}
