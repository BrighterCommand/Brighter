using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterBuilder UsePostgreSqlOutbox(
            this IBrighterBuilder brighterBuilder, RelationalDatabaseConfiguration configuration, Type connectionProvider = null, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));

            if (configuration is null)
                throw new ArgumentNullException($"{nameof(configuration)} cannot be null.", nameof(configuration));

            brighterBuilder.Services.AddSingleton<RelationalDatabaseConfiguration>(configuration);

            if (connectionProvider is object)
            {
                if (!typeof(IAmARelationalDbConnectionProvider).IsAssignableFrom(connectionProvider))
                    throw new Exception($"Unable to register provider of type {connectionProvider.GetType().Name}. Class does not implement interface {nameof(IAmARelationalDbConnectionProvider)}.");

                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider), connectionProvider, serviceLifetime));
            }

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), BuildPostgreSqlOutboxSync, serviceLifetime));

            return brighterBuilder;
        }

        /// <summary>
        /// Use this transaction provider to ensure that the Outbox and the Entity Store are correct
        /// </summary>
        /// <param name="brighterBuilder">Allows extension method</param>
        /// <param name="connectionProvider">What is the type of the connection provider. Must implement interface IPostgreSqlTransactionConnectionProvider</param>
        /// <param name="serviceLifetime">What is the lifetime of registered interfaces</param>
        /// <returns>Allows fluent syntax</returns>
        /// This is paired with Use Outbox (above) when required
        /// Registers the following
        /// -- IAmABoxTransactionConnectionProvider: the provider of a connection for any existing transaction
        public static IBrighterBuilder UsePostgreSqlTransactionConnectionProvider(
            this IBrighterBuilder brighterBuilder, Type connectionProvider,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));

            if (connectionProvider is null)
                throw new ArgumentNullException($"{nameof(connectionProvider)} cannot be null.", nameof(connectionProvider));

            if (!typeof(IAmATransactionConnectonProvider).IsAssignableFrom(connectionProvider))
                throw new Exception($"Unable to register provider of type {connectionProvider.GetType().Name}. Class does not implement interface {nameof(IAmATransactionConnectonProvider)}.");

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider), connectionProvider, serviceLifetime));

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
