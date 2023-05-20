using System;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterBuilder UsePostgreSqlOutbox(
            this IBrighterBuilder brighterBuilder, RelationalDatabaseConfiguration configuration, Type connectionProvider, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            if (!typeof(IAmARelationalDbConnectionProvider).IsAssignableFrom(connectionProvider))
                throw new Exception($"Unable to register provider of type {connectionProvider.Name}. Class does not implement interface {nameof(IAmARelationalDbConnectionProvider)}.");
            
            brighterBuilder.Services.AddSingleton<RelationalDatabaseConfiguration>(configuration);
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider), connectionProvider, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message, DbTransaction>), BuildPostgreSqlOutboxSync, serviceLifetime));

            return brighterBuilder;
        }

        /// <summary>
        /// Use this transaction provider to ensure that the Outbox and the Entity Store are correct
        /// </summary>
        /// <param name="brighterBuilder">Allows extension method</param>
        /// <param name="transactionProvider">What is the type of the connection provider. Must implement interface IPostgreSqlTransactionConnectionProvider</param>
        /// <param name="serviceLifetime">What is the lifetime of registered interfaces</param>
        /// <returns>Allows fluent syntax</returns>
        /// This is paired with Use Outbox (above) when required
        /// Registers the following
        /// -- IAmABoxTransactionConnectionProvider: the provider of a connection for any existing transaction
        public static IBrighterBuilder UsePostgreSqlTransactionConnectionProvider(
            this IBrighterBuilder brighterBuilder, 
            Type transactionProvider,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (transactionProvider is null)
                throw new ArgumentNullException($"{nameof(transactionProvider)} cannot be null.", nameof(transactionProvider));

            if (!typeof(IAmABoxTransactionProvider<DbTransaction>).IsAssignableFrom(transactionProvider))
                throw new Exception($"Unable to register provider of type {transactionProvider.Name}. Class does not implement interface {nameof(IAmABoxTransactionProvider<DbTransaction>)}.");
            
            if (!typeof(IAmATransactionConnectionProvider).IsAssignableFrom(transactionProvider))
                throw new Exception($"Unable to register provider of type {transactionProvider.Name}. Class does not implement interface {nameof(IAmATransactionConnectionProvider)}.");
 
            
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider<DbTransaction>), transactionProvider, serviceLifetime));
             brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmATransactionConnectionProvider), transactionProvider, serviceLifetime));
 
             return brighterBuilder;
 
        }

        private static PostgreSqlOutbox BuildPostgreSqlOutboxSync(IServiceProvider provider)
        {
            var config = provider.GetService<RelationalDatabaseConfiguration>();
            var connectionProvider = provider.GetService<IAmARelationalDbConnectionProvider>();

            return new PostgreSqlOutbox(config, connectionProvider);
        }
    }
}
