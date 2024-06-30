﻿using FluentMigrator.Runner;
using Greetings_MySqlMigrations.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GreetingsDb;

public static class GreetingsDbFactory
{
    public static void ConfigureMigration(IConfiguration configuration, IServiceCollection services)
    {
        ConfigureProductionDatabase(configuration, services);
    }

    private static void ConfigureProductionDatabase(IConfiguration configuration, IServiceCollection services)
    {
        string? greetingsDbType = configuration[DatabaseGlobals.DATABASE_TYPE_ENV];
        if (string.IsNullOrWhiteSpace(greetingsDbType))
            throw new InvalidOperationException("DbType is not set");

        DatabaseType databaseType = DbResolver.GetDatabaseType(greetingsDbType);

        switch (databaseType)
        {
            case DatabaseType.MySql:
                ConfigureMySql(configuration, services);
                break;
            case DatabaseType.MsSql:
                ConfigureMsSql(configuration, services);
                break;
            case DatabaseType.Postgres:
                ConfigurePostgreSql(configuration, services);
                break;
            case DatabaseType.Sqlite:
                ConfigureSqlite(configuration, services);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported");
        }
    }

    private static void ConfigureMsSql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddSqlServer()
                .WithGlobalConnectionString(ConnectionResolver.GreetingsDbConnectionString(configuration))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = DatabaseType.MsSql.ToString()
            });
    }

    private static void ConfigureMySql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddMySql5()
                .WithGlobalConnectionString(ConnectionResolver.GreetingsDbConnectionString(configuration))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = DatabaseType.MySql.ToString()
            });
    }

    private static void ConfigurePostgreSql(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddPostgres()
                .ConfigureGlobalProcessorOptions(opt => opt.ProviderSwitches = "Force Quote=false")
                .WithGlobalConnectionString(ConnectionResolver.GreetingsDbConnectionString(configuration))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = DatabaseType.Postgres.ToString()
            });
    }

    private static void ConfigureSqlite(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(c => c.AddSQLite()
                .WithGlobalConnectionString(ConnectionResolver.GreetingsDbConnectionString(configuration))
                .ScanIn(typeof(SqlInitialCreate).Assembly).For.Migrations()
            )
            .AddSingleton<IAmAMigrationConfiguration>(new MigrationConfiguration
            {
                DbType = DatabaseType.Sqlite.ToString()
            });
    }
}
