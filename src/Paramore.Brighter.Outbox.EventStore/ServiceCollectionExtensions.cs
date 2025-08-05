using System;
using EventStore.ClientAPI;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.EventStore
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterBuilder UseEventStoreOutbox(
            this IBrighterBuilder brighterBuilder, IEventStoreConnection connection, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<IEventStoreConnection>(connection);

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildEventStoreOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), BuildEventStoreOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildEventStoreOutbox, serviceLifetime));
             
            return brighterBuilder;
        }

        private static EventStoreOutboxSync BuildEventStoreOutbox(IServiceProvider provider)
        {
            var connection = provider.GetService<IEventStoreConnection>();

            return new EventStoreOutboxSync(connection);
        }
    }
}
