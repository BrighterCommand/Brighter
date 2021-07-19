﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.Outbox.MsSql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UseMsSqlOutbox(
            this IBrighterHandlerBuilder brighterBuilder, MsSqlConfiguration configuration, Type connectionProvider, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<MsSqlConfiguration>(configuration);
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IMsSqlConnectionProvider), connectionProvider, serviceLifetime));
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildMsSqlOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildMsSqlOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static MsSqlOutbox BuildMsSqlOutbox(IServiceProvider provider)
        {
            var connectionProvider = provider.GetService<IMsSqlConnectionProvider>();
            var config = provider.GetService<MsSqlConfiguration>();

            return new MsSqlOutbox(config, connectionProvider);
        }
    }
}
