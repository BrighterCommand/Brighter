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

    public class OutboxExtensions
    {
        public static (IAmAnOutbox, Type) MakeOutbox(
            IWebHostEnvironment env,
            DatabaseType databaseType,
            RelationalDatabaseConfiguration configuration)
        {
            (IAmAnOutbox, Type) outbox;
            if (env.IsDevelopment())
            {
                outbox = MakeSqliteOutBox(configuration);
            }
            else
            {
                switch (databaseType)
                {
                    case DatabaseType.MySql:
                        outbox = MakeMySqlOutbox(configuration);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown Db type for Outbox configuration");
                }
            }

            return outbox;
        }

        private static (IAmAnOutbox, Type)  MakeMySqlOutbox(RelationalDatabaseConfiguration configuration)
        {
            return (new MySqlOutbox(configuration), typeof(MySqlUnitOfWork));
        }

        private static (IAmAnOutbox, Type) MakeSqliteOutBox(RelationalDatabaseConfiguration configuration)
        {
            return (new SqliteOutbox(configuration), typeof(SqliteUnitOfWork));
        }
    }
}
