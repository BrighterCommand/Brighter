using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalutationsMigrations.Migrations;

namespace DbMaker;

public class SalutationsDbFactory
{
    public static void ConfigureMigration(HostBuilderContext hostContext, IServiceCollection services)
{
    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    BuildSalutationsDatabase(hostContext, DbResolver.GetDatabaseType(dbType), services);
}

static void BuildSalutationsDatabase(
    HostBuilderContext hostBuilderContext,
    Rdbms rdbms,
    IServiceCollection services)
{
    switch (rdbms)
    {
        case Rdbms.MySql:
            ConfigureMySql(hostBuilderContext, services);
            break;
        case Rdbms.MsSql:
            ConfigureMsSql(hostBuilderContext, services);
            break;
        case Rdbms.Postgres:
            ConfigurePostgreSql(hostBuilderContext, services);
            break;
        case Rdbms.Sqlite:
            ConfigureSqlite(hostBuilderContext, services);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(rdbms), "Database type is not supported");
    }
}

static void ConfigureMySql(HostBuilderContext hostContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddMySql5()
            .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Salutations))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigureMsSql(HostBuilderContext hostContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddSqlServer()
            .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Salutations))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigurePostgreSql(HostBuilderContext hostContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddPostgres()
            .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
            .WithGlobalConnectionString(ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Salutations))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigureSqlite(HostBuilderContext hostContext, IServiceCollection services)
{
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c =>
        {
            c.AddSQLite()
                .WithGlobalConnectionString(
                    ConnectionResolver.DbConnectionString(hostContext.Configuration, ApplicationType.Salutations))
                .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations();
        });
}

}
