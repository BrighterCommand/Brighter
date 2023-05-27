#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;
using System.Text.Json;
using System.Transactions;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Will add Brighter into the .NET IoC Container - ServiceCollection
        /// Registers the following with the service collection :-
        ///  - BrighterOptions - how should we configure Brighter
        ///  - Feature Switch Registry - optional if features switch support is desired
        ///  - Inbox - defaults to InMemoryInbox if none supplied 
        ///  - SubscriberRegistry - what handlers subscribe to what requests
        ///  - MapperRegistry - what mappers translate what messages
        /// </summary>
        /// <param name="services">The collection of services that we want to add registrations to</param>
        /// <param name="configure">A callback that defines what options to set when Brighter is built</param>
        /// <returns>A builder that can be used to populate the IoC container with handlers and mappers by inspection
        /// - used by built in factory from CommandProcessor</returns>
        /// <exception cref="ArgumentNullException">Thrown if we have no IoC provided ServiceCollection</exception>
        public static IBrighterBuilder AddBrighter(
            this IServiceCollection services,
            Action<BrighterOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new BrighterOptions();
            configure?.Invoke(options);
            services.TryAddSingleton<IBrighterOptions>(options);

            return BrighterHandlerBuilder(services, options);
        }
        
        /// <summary>
        /// This is public so that we can call it from <see cref="ServiceCollectionExtensions.AddServiceActivator"/>
        /// which allows that extension method to be called with a <see cref="ServiceActivatorOptions"/> configuration
        /// that derives from <see cref="BrighterOptions"/>.
        /// DON'T CALL THIS DIRECTLY
        /// Registers the following with the service collection :-
        ///  - BrighterOptions - how should we configure Brighter
        ///  - Feature Switch Registry - optional if features switch support is desired
        ///  - Inbox - defaults to InMemoryInbox if none supplied 
        ///  - SubscriberRegistry - what handlers subscribe to what requests
        ///  - MapperRegistry - what mappers translate what messages
        /// </summary>
        /// <param name="services">The collection of services that we want to add registrations to</param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static IBrighterBuilder BrighterHandlerBuilder(IServiceCollection services, BrighterOptions options)
        {
            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services, options.HandlerLifetime);
            services.TryAddSingleton(subscriberRegistry);

            var transformRegistry = new ServiceCollectionTransformerRegistry(services, options.TransformerLifetime);
            services.TryAddSingleton(transformRegistry);

            services.TryAdd(new ServiceDescriptor(typeof(IAmACommandProcessor),
                (serviceProvider) => (IAmACommandProcessor)BuildCommandProcessor(serviceProvider),
                options.CommandProcessorLifetime));

            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services, options.MapperLifetime);
            services.TryAddSingleton(mapperRegistry);

            if (options.FeatureSwitchRegistry != null)
                services.TryAddSingleton(options.FeatureSwitchRegistry);

            //Add the policy registry
            IPolicyRegistry<string> policyRegistry;
            if (options.PolicyRegistry == null) policyRegistry = new DefaultPolicy();
            else policyRegistry = AddDefaults(options.PolicyRegistry);

            return new ServiceCollectionBrighterBuilder(
                services,
                subscriberRegistry,
                mapperRegistry,
                transformRegistry,
                policyRegistry
            );
        }

        /// <summary>
        /// An external bus is the use of Message Oriented Middleware (MoM) to dispatch a message between a producer
        /// and a consumer. The assumption is that this  is being used for inter-process communication, for example the
        /// work queue pattern for distributing work, or between microservicves.
        /// NOTE: This external bus will use an in memory outbox to facilitate sending; this will not survive restarts
        /// of the service. Use the alternative constructor if you wish to provide an outbox that supports transactions
        /// that persist to a Db.
        /// Registers singletons with the service collection :-
        ///  - Producer - the Gateway wrapping access to Middleware
        ///  - UseRpc - do we want to use Rpc i.e. a command blocks waiting for a response, over middleware
        /// </summary>
        /// - An Event Bus - used to send message externally and contains:
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
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder UseExternalBus(
            this IBrighterBuilder brighterBuilder,
            Action<ExternalBusConfiguration> configure = null)
        {
            return UseExternalBus<CommittableTransaction>(brighterBuilder, configure);
        }

        /// <summary>
        /// An external bus is the use of Message Oriented Middleware (MoM) to dispatch a message between a producer
        /// and a consumer. The assumption is that this  is being used for inter-process communication, for example the
        /// work queue pattern for distributing work, or between microservicves
        /// Registers singletons with the service collection :-
        /// - An Event Bus - used to send message externally and contains:
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
        /// <param name="configure">A callback that allows you to configure <see cref="ExternalBusConfiguration"/> options</param>
        /// <param name="transactionProvider">The transaction provider for the outbox, can be null for in-memory default
        /// of <see cref="CommittableTransactionProvider"/> which you must set the generic type to <see cref="CommittableTransaction"/> for
        /// </param>
        /// <param name="serviceLifetime">The lifetime of the transaction provider</param>
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder UseExternalBus<TTransaction>(
            this IBrighterBuilder brighterBuilder,
            Action<ExternalBusConfiguration> configure,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));
            
            var busConfiguration = new ExternalBusConfiguration();
            configure?.Invoke(busConfiguration);
            brighterBuilder.Services.TryAddSingleton<IAmExternalBusConfiguration>(busConfiguration);

            //default to using System Transactions if nothing provided, so we always technically can share the outbox transaction
            Type transactionProvider = busConfiguration.TransactionProvider ?? typeof(CommittableTransactionProvider);

            if (transactionProvider.GenericTypeArguments[0] != typeof(TTransaction))
                throw new ConfigurationException(
                    $"Unable to register provider of type {transactionProvider.Name}. Generic type argument does not match {typeof(TTransaction).Name}.");

            if (!typeof(IAmABoxTransactionProvider<TTransaction>).IsAssignableFrom(transactionProvider))
                throw new ConfigurationException(
                    $"Unable to register provider of type {transactionProvider.Name}. Class does not implement interface {nameof(IAmABoxTransactionProvider<TTransaction>)}.");

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider<TTransaction>),
                transactionProvider, serviceLifetime));

            RegisterRelationalProviderServicesMaybe<TTransaction>(brighterBuilder, transactionProvider,
                serviceLifetime);

            return ExternalBusBuilder<TTransaction>(brighterBuilder, busConfiguration);
        }

        private static INeedARequestContext AddEventBus(IServiceProvider provider, INeedMessaging messagingBuilder,
            IUseRpc useRequestResponse)
        {
            var eventBus = provider.GetService<IAmAnExternalBusService>();
            var eventBusConfiguration = provider.GetService<IAmExternalBusConfiguration>();
            var messageMapperRegistry = provider.GetService<IAmAMessageMapperRegistry>();
            var messageTransformerFactory = provider.GetService<IAmAMessageTransformerFactory>();

            INeedARequestContext ret = null;
            if (eventBus == null) ret = messagingBuilder.NoExternalBus();
            if (eventBus != null && useRequestResponse.RPC)
            {
                ret = messagingBuilder.ExternalBus(
                    useRequestResponse.RPC ? ExternalBusType.RPC : ExternalBusType.FireAndForget,
                    eventBus,
                    messageMapperRegistry,
                    messageTransformerFactory,
                    eventBusConfiguration.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions);
            }
            else if (eventBus != null && useRequestResponse.RPC)
            {
                ret = messagingBuilder.ExternalBus(
                    useRequestResponse.RPC ? ExternalBusType.RPC : ExternalBusType.FireAndForget,
                    eventBus,
                    messageMapperRegistry,
                    messageTransformerFactory,
                    eventBusConfiguration.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions
                );
            }

            return ret;
        }

        private static IPolicyRegistry<string> AddDefaults(IPolicyRegistry<string> policyRegistry)
        {
            if (!policyRegistry.ContainsKey(CommandProcessor.RETRYPOLICY))
                throw new ConfigurationException(
                    "The policy registry is missing the CommandProcessor.RETRYPOLICY policy which is required");

            if (!policyRegistry.ContainsKey(CommandProcessor.CIRCUITBREAKER))
                throw new ConfigurationException(
                    "The policy registry is missing the CommandProcessor.CIRCUITBREAKER policy which is required");

            return policyRegistry;
        }

        private static object BuildCommandProcessor(IServiceProvider provider)
        {
            var loggerFactory = provider.GetService<ILoggerFactory>();
            ApplicationLogging.LoggerFactory = loggerFactory;

            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();
            var useRequestResponse = provider.GetService<IUseRpc>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory);

            var needHandlers = CommandProcessorBuilder.With();

            var featureSwitchRegistry = provider.GetService<IAmAFeatureSwitchRegistry>();

            if (featureSwitchRegistry != null)
                needHandlers = needHandlers.ConfigureFeatureSwitches(featureSwitchRegistry);

            var policyBuilder = needHandlers.Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            INeedARequestContext ret = AddEventBus(provider, messagingBuilder, useRequestResponse);

            var commandProcessor = ret
                .RequestContextFactory(options.RequestContextFactory)
                .Build();

            return commandProcessor;
        }

        private static IBrighterBuilder ExternalBusBuilder<TTransaction>(
            IBrighterBuilder brighterBuilder,
            IAmExternalBusConfiguration externalBusConfiguration
        )
        {
            if (externalBusConfiguration.ProducerRegistry == null)
                throw new ConfigurationException("An external bus must have an IAmAProducerRegistry");

            var serviceCollection = brighterBuilder.Services;

            serviceCollection.TryAddSingleton<IAmExternalBusConfiguration>(externalBusConfiguration);
            serviceCollection.TryAddSingleton<IAmAProducerRegistry>(externalBusConfiguration.ProducerRegistry);

            var outbox = externalBusConfiguration.Outbox;
            if (outbox == null)
            {
                outbox = new InMemoryOutbox();
                serviceCollection.TryAddSingleton<IAmAnOutboxSync<Message, CommittableTransaction>>(
                    (IAmAnOutboxSync<Message, CommittableTransaction>)outbox
                );
                serviceCollection.TryAddSingleton<IAmAnOutboxAsync<Message, CommittableTransaction>>(
                    (IAmAnOutboxAsync<Message, CommittableTransaction>)outbox
                );
            }
            else
            {
                if (outbox is IAmAnOutboxSync<Message, TTransaction> outboxSync)
                    serviceCollection.TryAddSingleton(outboxSync);
                if (outbox is IAmAnOutboxAsync<Message, TTransaction> outboxAsync)
                    serviceCollection.TryAddSingleton(outboxAsync);
            }

            if (externalBusConfiguration.UseRpc)
            {
                serviceCollection.TryAddSingleton<IUseRpc>(new UseRpc(externalBusConfiguration.UseRpc,
                    externalBusConfiguration.ReplyQueueSubscriptions));
            }

            var bus = new ExternalBusServices<Message, TTransaction>(
                producerRegistry: externalBusConfiguration.ProducerRegistry,
                policyRegistry: brighterBuilder.PolicyRegistry,
                outbox: outbox,
                outboxBulkChunkSize: externalBusConfiguration.OutboxBulkChunkSize,
                outboxTimeout: externalBusConfiguration.OutboxTimeout);

            serviceCollection.TryAddSingleton<IAmAnExternalBusService>(bus);

            return brighterBuilder;
        }

        /// <summary>
        /// Config the Json Serializer that is used inside of Brighter
        /// </summary>
        /// <param name="brighterBuilder">The Brighter Builder</param>
        /// <param name="configure">Action to configure the options</param>
        /// <returns>Brighter Builder</returns>
        public static IBrighterBuilder ConfigureJsonSerialisation(this IBrighterBuilder brighterBuilder,
            Action<JsonSerializerOptions> configure)
        {
            var options = new JsonSerializerOptions();

            configure.Invoke(options);

            JsonSerialisationOptions.Options = options;

            return brighterBuilder;
        }

        /// <summary>
        /// Registers message mappers with the registry. Normally you don't need to call this, it is called by the builder for Brighter or the Service Activator
        /// Visibility is required for use from both
        /// </summary>
        /// <param name="provider">The IoC container to request the message mapper registry from</param>
        /// <returns>The message mapper registry, populated with any message mappers from the ioC container</returns>
        public static MessageMapperRegistry MessageMapperRegistry(IServiceProvider provider)
        {
            var serviceCollectionMessageMapperRegistry = provider.GetService<ServiceCollectionMessageMapperRegistry>();

            var messageMapperRegistry = new MessageMapperRegistry(new ServiceProviderMapperFactory(provider));

            foreach (var messageMapper in serviceCollectionMessageMapperRegistry)
            {
                messageMapperRegistry.Add(messageMapper.Key, messageMapper.Value);
            }

            return messageMapperRegistry;
        }

        private static void RegisterRelationalProviderServicesMaybe<TTransaction>(
            IBrighterBuilder brighterBuilder,
            Type transactionProvider, ServiceLifetime serviceLifetime)
        {
            //not all box transaction providers are also relational connection providers
            if (typeof(IAmARelationalDbConnectionProvider).IsAssignableFrom(transactionProvider))
            {
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider),
                    transactionProvider, serviceLifetime));

                //register the combined interface just in case
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmATransactionConnectionProvider),
                    transactionProvider, serviceLifetime));
            }
        }

        /// <summary>
        /// Creates transforms. Normally you don't need to call this, it is called by the builder for Brighter or
        /// the Service Activator
        /// Visibility is required for use from both
        /// </summary>
        /// <param name="provider">The IoC container to build the transform factory over</param>
        /// <returns></returns>
        public static ServiceProviderTransformerFactory TransformFactory(IServiceProvider provider)
        {
            return new ServiceProviderTransformerFactory(provider);
        }
    }
}
