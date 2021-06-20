using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.MsSql.ConnectionFactories;

namespace Paramore.Brighter.Outbox.MsSql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UseMsSqlOutbox(
            this IBrighterHandlerBuilder brighterBuilder, MsSqlOutboxConfiguration configuration, Type connectionFactory, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<MsSqlOutboxConfiguration>(configuration);
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IMsSqlOutboxConnectionFactory), connectionFactory, serviceLifetime));
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildMsSqlOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildMsSqlOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static MsSqlOutbox BuildMsSqlOutbox(IServiceProvider provider)
        {
            var connectionFactory = provider.GetService<IMsSqlOutboxConnectionFactory>();
            var config = provider.GetService<MsSqlOutboxConfiguration>();

            return new MsSqlOutbox(config, connectionFactory);
        }
    }
}
