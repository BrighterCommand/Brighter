using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UsePostgreSqlOutbox(
            this IBrighterHandlerBuilder brighterBuilder, PostgreSqlOutboxConfiguration configuration, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<PostgreSqlOutboxConfiguration>(configuration);

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildDynamoDbOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildDynamoDbOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static PostgreSqlOutbox BuildDynamoDbOutbox(IServiceProvider provider)
        {
            var config = provider.GetService<PostgreSqlOutboxConfiguration>();

            return new PostgreSqlOutbox(config);
        }
    }
}
