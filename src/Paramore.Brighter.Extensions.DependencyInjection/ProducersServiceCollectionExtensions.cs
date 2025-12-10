#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring producers and external bus with the Brighter builder.
    /// </summary>
    public static class ProducersServiceCollectionExtensions
    {
        /// <summary>
        /// We use AddProducers to register an external bus, which is a bus that is not the Brighter In-Memory Bus.
        /// The external bus uses Message Oriented Middleware (MoM) to dispatch a message from a producer
        /// to a consumer.
        /// Registers singletons with the service collection :-
        ///     -- An Outbox Producer Mediator - used to send message externally via an Outbox:
        ///     -- Producer Registry - A list of producers we can send middleware messages with
        ///     -- Outbox - stores messages so that they can be written in the same transaction as entity writes
        ///     -- Outbox Transaction Provider - used to provide a transaction that spans the Outbox write and
        ///         your updates to your entities
        ///     -- RelationalDb Connection Provider - if your transaction provider is for a relational db we register this
        ///         interface to access your Db and make it available to your own classes
        ///     -- Transaction Connection Provider  - if your transaction provider is also a relational db connection
        ///         provider it will implement this interface which inherits from both
        ///     -- External Bus Configuration - the configuration parameters for an external bus, mainly used internally
        ///     -- UseRpc - do we want to use RPC i.e. a command blocks waiting for a response, over middleware.
        /// </summary>
        /// <param name="brighterBuilder">The Brighter builder to add this option to</param>
        /// <param name="configure">A callback that allows you to configure <see cref="ProducersConfiguration"/> options</param>
        /// <param name="serviceLifetime">The lifetime of the transaction provider</param>
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder AddProducers(
            this IBrighterBuilder brighterBuilder,
            Action<ProducersConfiguration> configure,
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException(nameof(brighterBuilder), $"{nameof(brighterBuilder)} cannot be null.");

            var busConfiguration = new ProducersConfiguration();
            configure?.Invoke(busConfiguration);

            ValidateProducersConfiguration(busConfiguration);

            brighterBuilder.Services.TryAddSingleton<IAmAPublicationFinder, FindPublicationByPublicationTopicOrRequestType>();
            brighterBuilder.Services.TryAddSingleton(busConfiguration.ProducerRegistry!);

            // Get transaction type using helper
            Type transactionProviderType = busConfiguration.TransactionProvider ?? typeof(InMemoryTransactionProvider);
            Type transactionType = TransactionProviderHelper.GetTransactionTypeOrThrow(transactionProviderType);

            // Register transaction provider
            RegisterTransactionProvider(brighterBuilder, transactionProviderType, transactionType, serviceLifetime);

            // Register connection provider if specified
            if (busConfiguration.ConnectionProvider != null)
                RegisterConnectionAndTransactionProvider(brighterBuilder, busConfiguration.ConnectionProvider, transactionProviderType, serviceLifetime);

            // Register outbox
            var outbox = busConfiguration.Outbox ?? new InMemoryOutbox(TimeProvider.System);
            RegisterOutbox(brighterBuilder, outbox, transactionType);

            // Register distributed lock
            var distributedLock = busConfiguration.DistributedLock ?? new InMemoryLock();
            brighterBuilder.Services.AddSingleton(distributedLock);

            // Register RPC configuration
            RegisterRpcConfiguration(brighterBuilder, busConfiguration);

            // Register configuration and mediator
            brighterBuilder.Services.TryAddSingleton<IAmProducersConfiguration>(busConfiguration);
            brighterBuilder.ResiliencePolicyRegistry ??= new ResiliencePipelineRegistry<string>().AddBrighterDefault();

            brighterBuilder.Services.TryAdd(new ServiceDescriptor(
                typeof(IAmAnOutboxProducerMediator),
                sp => BuildOutBoxProducerMediator(sp, transactionType, busConfiguration, brighterBuilder.ResiliencePolicyRegistry, outbox)
                    ?? throw new ConfigurationException("Unable to create an outbox producer mediator; are you missing a registration?"),
                ServiceLifetime.Singleton));

            return brighterBuilder;
        }

        /// <summary>
        /// We use AddProducers to register an external bus with access to IServiceProvider for resolving dependencies.
        /// The external bus uses Message Oriented Middleware (MoM) to dispatch a message from a producer to a consumer.
        /// Registers singletons with the service collection :-
        ///     -- An Outbox Producer Mediator - used to send message externally via an Outbox:
        ///     -- Producer Registry - A list of producers we can send middleware messages with
        ///     -- Outbox - stores messages so that they can be written in the same transaction as entity writes
        ///     -- Outbox Transaction Provider - used to provide a transaction that spans the Outbox write and
        ///         your updates to your entities
        ///     -- RelationalDb Connection Provider - if your transaction provider is for a relational db we register this
        ///         interface to access your Db and make it available to your own classes
        ///     -- Transaction Connection Provider  - if your transaction provider is also a relational db connection
        ///         provider it will implement this interface which inherits from both
        ///     -- External Bus Configuration - the configuration parameters for an external bus, mainly used internally
        ///     -- UseRpc - do we want to use RPC i.e. a command blocks waiting for a response, over middleware.
        /// </summary>
        /// <param name="brighterBuilder">The Brighter builder to add this option to</param>
        /// <param name="configure">A callback that allows you to configure <see cref="ProducersConfiguration"/> options with access to IServiceProvider</param>
        /// <param name="serviceLifetime">The lifetime of the transaction provider</param>
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder AddProducers(
            this IBrighterBuilder brighterBuilder,
            Action<ProducersConfiguration, IServiceProvider> configure,
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException(nameof(brighterBuilder), $"{nameof(brighterBuilder)} cannot be null.");
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            brighterBuilder.Services.AddSingleton<ProducersConfiguration>(sp =>
            {
                var busConfiguration = new ProducersConfiguration();
                configure(busConfiguration, sp);
                ValidateProducersConfiguration(busConfiguration);
                return busConfiguration;
            });

            brighterBuilder.Services.AddSingleton<IAmProducersConfiguration>(
                sp => sp.GetRequiredService<ProducersConfiguration>());

            brighterBuilder.Services.TryAddSingleton<IAmAPublicationFinder, FindPublicationByPublicationTopicOrRequestType>();
            brighterBuilder.ResiliencePolicyRegistry ??= new ResiliencePipelineRegistry<string>().AddBrighterDefault();

            // Register default transaction provider (will be overridden if custom one is specified)
            Type defaultTransactionProvider = typeof(InMemoryTransactionProvider);
            Type? defaultTransactionType = TransactionProviderHelper.GetTransactionType(defaultTransactionProvider);

            if (defaultTransactionType != null)
            {
                RegisterTransactionProvider(brighterBuilder, defaultTransactionProvider, defaultTransactionType, serviceLifetime);
            }

            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAProducerRegistry),
                sp =>
                {
                    var config = sp.GetRequiredService<ProducersConfiguration>();
                    return config.ProducerRegistry!;
                },
                ServiceLifetime.Singleton));

            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IDistributedLock),
                sp =>
                {
                    var config = sp.GetRequiredService<ProducersConfiguration>();
                    return config.DistributedLock ?? new InMemoryLock();
                },
                ServiceLifetime.Singleton));

            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IUseRpc),
                sp =>
                {
                    var config = sp.GetRequiredService<ProducersConfiguration>();
                    return config.UseRpc
                        ? new UseRpc(config.UseRpc, config.ReplyQueueSubscriptions!)
                        : new UseRpc(false, null!);
                },
                ServiceLifetime.Singleton));

            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAnOutboxProducerMediator),
                serviceProvider =>
                {
                    var busConfiguration = serviceProvider.GetRequiredService<ProducersConfiguration>();

                    Type transactionProviderType = busConfiguration.TransactionProvider ?? typeof(InMemoryTransactionProvider);
                    Type transactionType = TransactionProviderHelper.GetTransactionTypeOrThrow(transactionProviderType);

                    var outbox = busConfiguration.Outbox ?? new InMemoryOutbox(TimeProvider.System);
                    ValidateOutbox(outbox, transactionType);

                    return BuildOutBoxProducerMediator(
                        serviceProvider, transactionType, busConfiguration, brighterBuilder.ResiliencePolicyRegistry, outbox
                    ) ?? throw new ConfigurationException("Unable to create an outbox producer mediator; are you missing a registration?");
                },
                ServiceLifetime.Singleton));

            return brighterBuilder;
        }

        /// <summary>
        /// Set a default <see cref="IAmAPublicationFinder"/>
        /// </summary>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="lifetime">The service lifetime for the publication finder</param>
        /// <typeparam name="T">The publication finder type</typeparam>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UsePublicationFinder<T>(this IBrighterBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Transient)
            where T : class, IAmAPublicationFinder
        {
            builder.Services.Add(new ServiceDescriptor(typeof(IAmAPublicationFinder), typeof(T), lifetime));
            return builder;
        }

        /// <summary>
        /// Set a default <see cref="IAmAPublicationFinder"/>
        /// </summary>
        /// <param name="builder">The Brighter builder</param>
        /// <param name="instance">The publication finder instance</param>
        /// <typeparam name="T">The publication finder type</typeparam>
        /// <returns>The builder for chaining</returns>
        public static IBrighterBuilder UsePublicationFinder<T>(this IBrighterBuilder builder, T instance)
            where T : class, IAmAPublicationFinder
        {
            builder.Services.AddSingleton<IAmAPublicationFinder>(instance);
            return builder;
        }

        #region Private Helper Methods

        private static void ValidateProducersConfiguration(ProducersConfiguration config)
        {
            if (config.ProducerRegistry == null)
                throw new ConfigurationException("An external bus must have an IAmAProducerRegistry");

            if (config.UseRpc && config.ReplyQueueSubscriptions == null)
                throw new ConfigurationException("If the you configure RPC, you must configure the ReplyQueueSubscriptions");
        }

        private static void RegisterTransactionProvider(
            IBrighterBuilder builder,
            Type transactionProviderType,
            Type transactionType,
            ServiceLifetime lifetime)
        {
            var boxProviderType = typeof(IAmABoxTransactionProvider<>).MakeGenericType(transactionType);
            builder.Services.Add(new ServiceDescriptor(boxProviderType, transactionProviderType, lifetime));
            builder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider), transactionProviderType, lifetime));
        }

        private static void RegisterOutbox(IBrighterBuilder builder, IAmAnOutbox outbox, Type transactionType)
        {
            var syncOutboxType = typeof(IAmAnOutboxSync<,>).MakeGenericType(typeof(Message), transactionType);
            var asyncOutboxType = typeof(IAmAnOutboxAsync<,>).MakeGenericType(typeof(Message), transactionType);

            var outboxInterfaces = outbox.GetType().GetInterfaces();
            var hasSyncOutbox = outboxInterfaces.Any(i => i.IsGenericType && i == syncOutboxType);
            var hasAsyncOutbox = outboxInterfaces.Any(i => i.IsGenericType && i == asyncOutboxType);

            if (!hasSyncOutbox && !hasAsyncOutbox)
            {
                throw new ConfigurationException(
                    $"Unable to register outbox of type {outbox.GetType().Name} - no transaction provider has been registered that matches the outbox's transaction type");
            }

            builder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox), _ => outbox, ServiceLifetime.Singleton));

            if (hasSyncOutbox)
                builder.Services.Add(new ServiceDescriptor(syncOutboxType, _ => outbox, ServiceLifetime.Singleton));

            if (hasAsyncOutbox)
                builder.Services.Add(new ServiceDescriptor(asyncOutboxType, _ => outbox, ServiceLifetime.Singleton));
        }

        private static void ValidateOutbox(IAmAnOutbox outbox, Type transactionType)
        {
            var syncOutboxType = typeof(IAmAnOutboxSync<,>).MakeGenericType(typeof(Message), transactionType);
            var asyncOutboxType = typeof(IAmAnOutboxAsync<,>).MakeGenericType(typeof(Message), transactionType);

            var outboxInterfaces = outbox.GetType().GetInterfaces();
            var hasSyncOutbox = outboxInterfaces.Any(i => i.IsGenericType && i == syncOutboxType);
            var hasAsyncOutbox = outboxInterfaces.Any(i => i.IsGenericType && i == asyncOutboxType);

            if (!hasSyncOutbox && !hasAsyncOutbox)
            {
                throw new ConfigurationException(
                    $"Unable to register outbox of type {outbox.GetType().Name} - no transaction provider has been registered that matches the outbox's transaction type");
            }
        }

        private static void RegisterRpcConfiguration(IBrighterBuilder builder, ProducersConfiguration config)
        {
            if (config.UseRpc)
                builder.Services.TryAddSingleton<IUseRpc>(new UseRpc(config.UseRpc, config.ReplyQueueSubscriptions!));
        }

        private static void RegisterConnectionAndTransactionProvider(IBrighterBuilder brighterBuilder,
            Type connectionProvider,
            Type transactionProvider,
            ServiceLifetime serviceLifetime)
        {
            var connectionProviderInterface = GetConnectionProviderInterface(connectionProvider);
            if (connectionProviderInterface != null)
            {
                brighterBuilder.Services.TryAdd(new ServiceDescriptor(connectionProviderInterface, connectionProvider, serviceLifetime));

                var transactionProviderInterface = GetTransactionInterface(transactionProvider, connectionProviderInterface);
                if (transactionProviderInterface != null)
                {
                    brighterBuilder.Services.TryAdd(new ServiceDescriptor(transactionProviderInterface, transactionProvider, serviceLifetime));
                }
            }
            return;

            static Type? GetConnectionProviderInterface(Type type)
            {
                // all connection provider interfaces must be extended from IAmAConnectionProvider
                var interfaces = GetInterfaces(type);
                foreach (var @interface in interfaces)
                {
                    if (typeof(IAmAConnectionProvider).IsAssignableFrom(@interface)
                        && !typeof(IAmABoxTransactionProvider).IsAssignableFrom(@interface))
                    {
                        return @interface;
                    }
                }

                return null;
            }

            static Type? GetTransactionInterface(Type type, Type connectionProvider)
            {
                // All Brighter transaction provider interface must be extended from connection provider and IAmABoxTransactionProvider
                var interfaces = GetInterfaces(type);
                foreach (var @interface in interfaces)
                {
                    if (connectionProvider.IsAssignableFrom(@interface)
                        && typeof(IAmABoxTransactionProvider).IsAssignableFrom(@interface))
                    {
                        return @interface;
                    }
                }

                return null;
            }

            static IEnumerable<Type> GetInterfaces(Type type)
            {
                var interfaces = type.GetInterfaces().AsEnumerable();

                if (type.BaseType != null)
                {
                    interfaces = interfaces.Concat(GetInterfaces(type.BaseType));
                }

                return interfaces;
            }
        }

        private static IAmAnOutboxProducerMediator? BuildOutBoxProducerMediator(
            IServiceProvider serviceProvider,
            Type transactionType,
            ProducersConfiguration busConfiguration,
            ResiliencePipelineRegistry<string>? resiliencePipelineRegistry,
            IAmAnOutbox outbox)
        {
            //Because the bus has specialized types as members, we need to create the bus type dynamically
            //again to prevent someone configuring Brighter from having to pass generic types
            var busType = typeof(OutboxProducerMediator<,>).MakeGenericType(typeof(Message), transactionType);

            return (IAmAnOutboxProducerMediator?)Activator.CreateInstance(
                busType,
                busConfiguration.ProducerRegistry,
                resiliencePipelineRegistry,
                ServiceCollectionExtensions.MessageMapperRegistry(serviceProvider),
                ServiceCollectionExtensions.TransformFactory(serviceProvider),
                ServiceCollectionExtensions.TransformFactoryAsync(serviceProvider),
                ServiceCollectionExtensions.Tracer(serviceProvider),
                ServiceCollectionExtensions.PublicationFinder(serviceProvider),
                outbox,
                ServiceCollectionExtensions.OutboxCircuitBreaker(serviceProvider),
                ServiceCollectionExtensions.RequestContextFactory(serviceProvider),
                busConfiguration.OutboxTimeout,
                busConfiguration.MaxOutStandingMessages,
                busConfiguration.MaxOutStandingCheckInterval,
                busConfiguration.OutBoxBag,
                TimeProvider.System,
                busConfiguration.InstrumentationOptions);
        }

        #endregion
    }
}
