using System;
using EventStore.ClientAPI;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.EventStore
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UseEventStoreOutbox(
            this IBrighterHandlerBuilder brighterBuilder, IEventStoreConnection connection, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<IEventStoreConnection>(connection);

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildEventStoreOutboxOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildEventStoreOutboxOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static EventStoreOutbox BuildEventStoreOutboxOutbox(IServiceProvider provider)
        {
            var connection = provider.GetService<IEventStoreConnection>();

            return new EventStoreOutbox(connection);
        }
    }
}
