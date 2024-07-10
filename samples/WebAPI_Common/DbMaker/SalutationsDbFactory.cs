using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Salutations_Migrations.Migrations;

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
    DatabaseType databaseType,
    IServiceCollection services)
{
    switch (databaseType)
    {
        case DatabaseType.MySql:
            ConfigureMySql(hostBuilderContext, services);
            break;
        case DatabaseType.MsSql:
            ConfigureMsSql(hostBuilderContext, services);
            break;
        case DatabaseType.Postgres:
            ConfigurePostgreSql(hostBuilderContext, services);
            break;
        case DatabaseType.Sqlite:
            ConfigureSqlite(hostBuilderContext, services);
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
    }
}

static void ConfigureMySql(HostBuilderContext hostContext, IServiceCollection services)
{
    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddMySql5()
            .WithGlobalConnectionString(ConnectionResolver.GetSalutationsDbConnectionString(hostContext.Configuration,
                DbResolver.GetDatabaseType(dbType)))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigureMsSql(HostBuilderContext hostContext, IServiceCollection services)
{
    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddSqlServer()
            .WithGlobalConnectionString(ConnectionResolver.GetSalutationsDbConnectionString(hostContext.Configuration,
                DbResolver.GetDatabaseType(dbType)))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigurePostgreSql(HostBuilderContext hostContext, IServiceCollection services)
{
    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddPostgres()
            .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
            .WithGlobalConnectionString(ConnectionResolver.GetSalutationsDbConnectionString(hostContext.Configuration,
                DbResolver.GetDatabaseType(dbType)))
            .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations()
        );
}

static void ConfigureSqlite(HostBuilderContext hostContext, IServiceCollection services)
{
    string? dbType = hostContext.Configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
    if (string.IsNullOrWhiteSpace(dbType))
        throw new InvalidOperationException("DbType is not set");

    services
        .AddFluentMigratorCore()
        .ConfigureRunner(c =>
        {
            c.AddSQLite()
                .WithGlobalConnectionString(
                    ConnectionResolver.GetSalutationsDbConnectionString(hostContext.Configuration,
                        DbResolver.GetDatabaseType(dbType)))
                .ScanIn(typeof(SqlInitialMigrations).Assembly).For.Migrations();
        });
}

}
