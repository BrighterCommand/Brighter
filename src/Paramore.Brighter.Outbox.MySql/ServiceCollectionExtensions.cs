using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.MySql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UseMySqlOutbox(
            this IBrighterHandlerBuilder brighterBuilder, MySqlOutboxConfiguration configuration, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<MySqlOutboxConfiguration>(configuration);

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildMySqlOutboxOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildMySqlOutboxOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static MySqlOutbox BuildMySqlOutboxOutbox(IServiceProvider provider)
        {
            var config = provider.GetService<MySqlOutboxConfiguration>();

            return new MySqlOutbox(config);
        }
    }
}
