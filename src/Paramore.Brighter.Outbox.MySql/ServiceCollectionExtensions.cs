using System;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MySql;

namespace Paramore.Brighter.Outbox.MySql
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Use MySql for the Outbox
        /// </summary>
        /// <param name="brighterBuilder">Allows extension method syntax</param>
        /// <param name="configuration">The connection for the Db and name of the Outbox table</param>
        /// <param name="transactionProvider">The provider of transactions for the outbox to participate in</param>
        /// <param name="outboxBulkChunkSize">What size should we chunk bulk work in</param>
        /// <param name="outboxTimeout">What is the timeout in ms for the Outbox</param>
        /// <param name="serviceLifetime">What is the lifetime of the services that we add (outbox always singleton)</param>
        /// <returns>Allows fluent syntax</returns>
        /// Registers the following
        /// -- MySqlOutboxConfiguration: connection string and outbox name
        /// -- IAmARelationalDbConnectionProvider: lets us get a connection for the outbox that matches the entity store
        /// -- IAmAnOutbox: an outbox to store messages we want to send
        /// -- IAmAnOutboxSync&lt;Message, DbTransaction&gt;>: an outbox to store messages we want to send
         /// -- IAmAnOutboxAsync&lt;Message, DbTransaction&gt;: an outbox to store messages we want to send
        /// -- IAmABoxTransactionConnectionProvider: the provider of a connection for any existing transaction
        public static IBrighterBuilder UseMySqlOutbox(
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
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider), typeof(MySqlConnectionProvider), serviceLifetime));
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider<DbTransaction>), transactionProvider, serviceLifetime));
             
            //register the combined interface just in case
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmATransactionConnectionProvider), transactionProvider, serviceLifetime));

            var outbox = new MySqlOutbox(configuration, new MySqlConnectionProvider(configuration)); 
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message, DbTransaction>), outbox));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message, DbTransaction>), outbox));
             
            return brighterBuilder.UseExternalOutbox<Message, DbTransaction>(outbox, outboxBulkChunkSize, outboxTimeout);
        }
    }
}
