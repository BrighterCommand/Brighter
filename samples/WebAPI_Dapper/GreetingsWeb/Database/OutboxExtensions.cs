using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySql.Dapper;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.Dapper;
using UnitOfWork = Paramore.Brighter.MySql.Dapper.UnitOfWork;

namespace GreetingsWeb.Database;

public static class OutboxExtensions
{
    public static IBrighterBuilder AddOutbox(this IBrighterBuilder brighterBuilder, IWebHostEnvironment env, DatabaseType databaseType,
        string dbConnectionString, string outBoxTableName)
    {
        if (env.IsDevelopment())
        {
            AddSqliteOutBox(brighterBuilder, dbConnectionString, outBoxTableName);
        }
        else
        {
            switch (databaseType)
            {
                case DatabaseType.MySql:
                    AddMySqlOutbox(brighterBuilder, dbConnectionString, outBoxTableName);
                    break;
                default:
                    throw new InvalidOperationException("Unknown Db type for Outbox configuration");
            }
        }
        return brighterBuilder;
    }

    private static void AddMySqlOutbox(IBrighterBuilder brighterBuilder, string dbConnectionString, string outBoxTableName)
    {
        brighterBuilder.UseMySqlOutbox(
                new RelationalDatabaseConfiguration(dbConnectionString, outBoxTableName), 
                typeof(MySqlConnectionProvider),
                ServiceLifetime.Singleton)
            .UseMySqTransactionConnectionProvider(typeof(Paramore.Brighter.MySql.Dapper.MySqlDapperConnectionProvider), ServiceLifetime.Scoped)
            .UseOutboxSweeper();
    }

    private static void AddSqliteOutBox(IBrighterBuilder brighterBuilder, string dbConnectionString, string outBoxTableName)
    {
        brighterBuilder.UseSqliteOutbox(
                new RelationalDatabaseConfiguration(dbConnectionString, outBoxTableName), 
                typeof(SqliteConnectionProvider),
                ServiceLifetime.Singleton)
            .UseSqliteTransactionConnectionProvider(typeof(Paramore.Brighter.Sqlite.Dapper.SqliteDapperConnectionProvider), ServiceLifetime.Scoped)
            .UseOutboxSweeper(options =>
            {
                options.TimerInterval = 5;
                options.MinimumMessageAge = 5000;
            });
    }
}
