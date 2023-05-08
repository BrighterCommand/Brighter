using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;

namespace GreetingsWeb.Database
{

    public static class OutboxExtensions
    {
        public static IBrighterBuilder AddOutbox(
            this IBrighterBuilder brighterBuilder,
            IWebHostEnvironment env,
            DatabaseType databaseType,
            RelationalDatabaseConfiguration configuration)
        {
            if (env.IsDevelopment())
            {
                AddSqliteOutBox(brighterBuilder, configuration);
            }
            else
            {
                switch (databaseType)
                {
                    case DatabaseType.MySql:
                        AddMySqlOutbox(brighterBuilder, configuration);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown Db type for Outbox configuration");
                }
            }

            return brighterBuilder;
        }

        private static void AddMySqlOutbox(IBrighterBuilder brighterBuilder,
            RelationalDatabaseConfiguration configuration)
        {
            brighterBuilder.UseMySqlOutbox(
                    //new MySqlConfiguration(dbConnectionString, outBoxTableName, binaryMessagePayload: binaryMessagePayload), 
                    configuration,
                    typeof(MySqlConnectionProvider),
                    ServiceLifetime.Singleton)
                .UseMySqTransactionConnectionProvider(
                    typeof(Paramore.Brighter.MySql.MySqlConnectionProvider), ServiceLifetime.Scoped)
                .UseOutboxSweeper();
        }

        private static void AddSqliteOutBox(IBrighterBuilder brighterBuilder,
            RelationalDatabaseConfiguration configuration)
        {
            brighterBuilder.UseSqliteOutbox(
                    configuration,
                    typeof(SqliteConnectionProvider),
                    ServiceLifetime.Singleton)
                .UseSqliteTransactionConnectionProvider(
                    typeof(Paramore.Brighter.Sqlite.SqliteConnectionProvider), ServiceLifetime.Scoped)
                .UseOutboxSweeper(options =>
                {
                    options.TimerInterval = 5;
                    options.MinimumMessageAge = 5000;
                });
        }
    }
}
