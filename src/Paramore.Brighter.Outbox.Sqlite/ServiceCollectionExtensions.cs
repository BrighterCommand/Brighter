using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.Sqlite
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UseSqliteOutbox(
            this IBrighterHandlerBuilder brighterBuilder, SqliteOutboxConfiguration configuration, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<SqliteOutboxConfiguration>(configuration);

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildSqliteOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildSqliteOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static SqliteOutbox BuildSqliteOutbox(IServiceProvider provider)
        {
            var config = provider.GetService<SqliteOutboxConfiguration>();

            return new SqliteOutbox(config);
        }
    }
}
