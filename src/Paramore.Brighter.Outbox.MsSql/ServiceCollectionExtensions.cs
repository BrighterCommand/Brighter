using System;
using System.Data.Common;
using System.Runtime.CompilerServices;
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
            this IBrighterBuilder brighterBuilder, 
            RelationalDatabaseConfiguration configuration,
            Type transactionProvider,
            int outboxBulkChunkSize = 100,
            int outboxTimeout = 300,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped
            )
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));

            if (transactionProvider is null)
                throw new ArgumentNullException($"{nameof(transactionProvider)} cannot be null.", nameof(transactionProvider));

            if (!typeof(IAmATransactionConnectionProvider).IsAssignableFrom(transactionProvider))
                throw new Exception($"Unable to register provider of type {transactionProvider.Name}. Class does not implement interface {nameof(IAmATransactionConnectionProvider)}.");
            
            brighterBuilder.Services.AddSingleton<RelationalDatabaseConfiguration>(configuration);
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider), typeof(MsSqlAuthConnectionProvider), serviceLifetime));
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider<DbTransaction>), transactionProvider, serviceLifetime));
             
            //register the combined interface just in case
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmATransactionConnectionProvider), transactionProvider, serviceLifetime));

            var outbox = new MsSqlOutbox(configuration, new MsSqlAuthConnectionProvider(configuration)); 
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message, DbTransaction>), outbox));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message, DbTransaction>), outbox));
             
            return brighterBuilder.UseExternalOutbox<Message, DbTransaction>(outbox, outboxBulkChunkSize, outboxTimeout);
        }
    }
}
