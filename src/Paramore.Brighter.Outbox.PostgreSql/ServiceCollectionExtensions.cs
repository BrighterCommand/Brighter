using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterBuilder UsePostgreSqlOutbox(
            this IBrighterBuilder brighterBuilder, PostgreSqlOutboxConfiguration configuration, Type connectionProvider = null, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));

            if (configuration is null)
                throw new ArgumentNullException($"{nameof(configuration)} cannot be null.", nameof(configuration));

            brighterBuilder.Services.AddSingleton<PostgreSqlOutboxConfiguration>(configuration);

            if (connectionProvider is object)
            {
                if (!typeof(IPostgreSqlConnectionProvider).IsAssignableFrom(connectionProvider))
                    throw new Exception($"Unable to register provider of type {connectionProvider.GetType().Name}. Class does not implement interface {nameof(IPostgreSqlConnectionProvider)}.");

                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IPostgreSqlConnectionProvider), connectionProvider, serviceLifetime));
            }

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), BuildPostgreSqlOutboxSync, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildPostgreSqlOutboxAsync, serviceLifetime));

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

            if (!typeof(IPostgreSqlTransactionConnectionProvider).IsAssignableFrom(connectionProvider))
                throw new Exception($"Unable to register provider of type {connectionProvider.GetType().Name}. Class does not implement interface {nameof(IPostgreSqlTransactionConnectionProvider)}.");

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionConnectionProvider), connectionProvider, serviceLifetime));

            return brighterBuilder;
        }

        private static PostgreSqlOutboxSync BuildPostgreSqlOutboxSync(IServiceProvider provider)
        {
            var config = provider.GetService<PostgreSqlOutboxConfiguration>();
            var connectionProvider = provider.GetService<IPostgreSqlConnectionProvider>();

            return new PostgreSqlOutboxSync(config, connectionProvider);
        }

        private static PostgreSqlOutboxAsync BuildPostgreSqlOutboxAsync(IServiceProvider provider)
        {
            var config = provider.GetService<PostgreSqlOutboxConfiguration>();
            var connectionProvider = provider.GetService<IPostgreSqlConnectionProvider>();

            return new PostgreSqlOutboxAsync(config, connectionProvider);
        }
    }
}
