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
        /// <param name="connectionProvider">What is the type for the class that lets us obtain connections for the Sqlite database</param>
        /// <param name="serviceLifetime">What is the lifetime of the services that we add</param>
        /// <returns>Allows fluent syntax</returns>
        /// Registers the following
        /// -- MySqlOutboxConfiguration: connection string and outbox name
        /// -- IAmARelationalDbConnectionProvider: lets us get a connection for the outbox that matches the entity store
        /// -- IAmAnOutbox<Message>: an outbox to store messages we want to send
        /// -- IAmAnOutboxAsync<Message>: an outbox to store messages we want to send
        /// -- IAmAnOutboxViewer<Message>: Lets us read the entries in the outbox
        /// -- IAmAnOutboxViewerAsync<Message>: Lets us read the entries in the outbox
        public static IBrighterBuilder UseMySqlOutbox(
            this IBrighterBuilder brighterBuilder, RelationalDatabaseConfiguration configuration, Type connectionProvider, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<RelationalDatabaseConfiguration>(configuration);
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider), connectionProvider, serviceLifetime));

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message, DbTransaction>), BuildMySqlOutboxOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message, DbTransaction>), BuildMySqlOutboxOutbox, serviceLifetime));
             
            return brighterBuilder;
        }
        
         /// <summary>
         /// Use this transaction provider to ensure that the Outbox and the Entity Store are correct
         /// </summary>
         /// <param name="brighterBuilder">Allows extension method</param>
         /// <param name="transactionProvider">What is the type of the transaction provider</param>
         /// <param name="serviceLifetime">What is the lifetime of registered interfaces</param>
         /// <returns>Allows fluent syntax</returns>
         /// This is paired with Use Outbox (above) when required
         /// Registers the following
         /// -- IAmABoxTransactionConnectionProvider: the provider of a connection for any existing transaction
         public static IBrighterBuilder UseMySqTransactionConnectionProvider(
             this IBrighterBuilder brighterBuilder, 
             Type transactionProvider,
             ServiceLifetime serviceLifetime = ServiceLifetime.Scoped
             )
         {
            if (transactionProvider is null)
                throw new ArgumentNullException($"{nameof(transactionProvider)} cannot be null.", nameof(transactionProvider));

            if (!typeof(IAmABoxTransactionProvider<DbTransaction>).IsAssignableFrom(transactionProvider))
                throw new Exception($"Unable to register provider of type {transactionProvider.Name}. Class does not implement interface {nameof(IAmABoxTransactionProvider<DbTransaction>)}.");
            
            if (!typeof(IAmATransactionConnectionProvider).IsAssignableFrom(transactionProvider))
                throw new Exception($"Unable to register provider of type {transactionProvider.Name}. Class does not implement interface {nameof(IAmATransactionConnectionProvider)}.");
            
             //register the specific interface
             brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider<DbTransaction>), transactionProvider, serviceLifetime));
             
             //register the combined interface just in case
             brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmATransactionConnectionProvider), transactionProvider, serviceLifetime));
 
             return brighterBuilder;
         }
       
        private static MySqlOutbox BuildMySqlOutboxOutbox(IServiceProvider provider)
        {
            var config = provider.GetService<RelationalDatabaseConfiguration>();
            var connectionProvider = provider.GetService<IAmARelationalDbConnectionProvider>();

            return new MySqlOutbox(config, connectionProvider);
        }
    }
}
