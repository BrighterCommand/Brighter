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
            
            if (serviceLifetime == ServiceLifetime.Scoped)
            {
                brighterBuilder.Services.AddScoped<IAmAnOutbox<Message>, MsSqlOutbox>();
                brighterBuilder.Services.AddScoped<IAmAnOutboxAsync<Message>, MsSqlOutbox>();
            }
            else
            {
                brighterBuilder.Services.AddSingleton<IAmAnOutbox<Message>>();
                brighterBuilder.Services.AddSingleton<IAmAnOutboxAsync<Message>>();
            }

            return brighterBuilder;
        }
    }
}
