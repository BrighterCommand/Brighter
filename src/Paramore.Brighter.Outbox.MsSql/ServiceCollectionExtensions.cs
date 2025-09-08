using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.Outbox.MsSql
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Use MsSql for the Outbox
        /// </summary>
        /// <param name="brighterBuilder">Allows extension method syntax</param>
        /// <param name="configuration">The connection for the Db and name of the Outbox table</param>
        /// <param name="connectionProvider">What is the type for the class that lets us obtain connections for the Sqlite database</param>
        /// <param name="serviceLifetime">What is the lifetime of the services that we add</param>
        /// <returns>Allows fluent syntax</returns>
        /// -- Registers the following
        /// -- MsSqlConfiguration: connection string and outbox name
        /// -- IMsSqlConnectionProvider: lets us get a connection for the outbox that matches the entity store
        /// -- IAmAnOutbox<Message>: an outbox to store messages we want to send
        /// -- IAmAnOutboxAsync<Message>: an outbox to store messages we want to send
        /// -- IAmAnOutboxViewer<Message>: Lets us read the entries in the outbox
        /// -- IAmAnOutboxViewerAsync<Message>: Lets us read the entries in the outbox
        public static IBrighterBuilder UseMsSqlOutbox(
            this IBrighterBuilder brighterBuilder, MsSqlConfiguration configuration, Type connectionProvider, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton, int outboxBulkChunkSize = 100)
        {
            brighterBuilder.Services.AddSingleton<MsSqlConfiguration>(configuration);
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IMsSqlConnectionProvider), connectionProvider, serviceLifetime));
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildMsSqlOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), BuildMsSqlOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildMsSqlOutbox, serviceLifetime));
            
            //Set chunk size - TODO: Bring this inline
            brighterBuilder.UseExternalOutbox(null, outboxBulkChunkSize);
 
            return brighterBuilder;
        }

         /// <summary>
         /// Use this transaction provider to ensure that the Outbox and the Entity Store are correct
         /// </summary>
         /// <param name="brighterBuilder">Allows extension method</param>
         /// <param name="connectionProvider">What is the type of the connection provider</param>
         /// <param name="serviceLifetime">What is the lifetime of registered interfaces</param>
         /// <returns>Allows fluent syntax</returns>
         /// This is paired with Use Outbox (above) when required
         /// Registers the following
         /// -- IAmABoxTransactionConnectionProvider: the provider of a connection for any existing transaction
         public static IBrighterBuilder UseMsSqlTransactionConnectionProvider(
            this IBrighterBuilder brighterBuilder, Type connectionProvider,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionConnectionProvider), connectionProvider, serviceLifetime));

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
