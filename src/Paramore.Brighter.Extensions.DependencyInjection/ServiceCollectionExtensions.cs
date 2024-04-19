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
using Paramore.Brighter.DynamoDb;
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

            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services, options.MapperLifetime);
            services.TryAddSingleton(mapperRegistry);

            if (options.FeatureSwitchRegistry != null)
                services.TryAddSingleton(options.FeatureSwitchRegistry);

            //Add the policy registry
            IPolicyRegistry<string> policyRegistry;
            if (options.PolicyRegistry == null) policyRegistry = new DefaultPolicy();
            else policyRegistry = AddDefaults(options.PolicyRegistry);

            services.TryAdd(new ServiceDescriptor(typeof(IAmACommandProcessor),
                (serviceProvider) => (IAmACommandProcessor)BuildCommandProcessor(serviceProvider),
                options.CommandProcessorLifetime));

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
        public static IBrighterBuilder UseExternalBus(
            this IBrighterBuilder brighterBuilder,
            Action<ExternalBusConfiguration> configure,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            if (brighterBuilder is null)
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));

            var busConfiguration = new ExternalBusConfiguration();
            configure?.Invoke(busConfiguration);
            
            if (busConfiguration.ProducerRegistry == null)
                throw new ConfigurationException("An external bus must have an IAmAProducerRegistry");
            
            brighterBuilder.Services.TryAddSingleton(busConfiguration.ProducerRegistry);

            //default to using System Transactions if nothing provided, so we always technically can share the outbox transaction
            Type transactionProvider = busConfiguration.TransactionProvider ?? typeof(CommittableTransactionProvider);

            //Find the transaction type from the provider
            Type transactionProviderInterface = typeof(IAmABoxTransactionProvider<>);
            Type transactionType = null;
            foreach (Type i in transactionProvider.GetInterfaces())
                if (i.IsGenericType && i.GetGenericTypeDefinition() == transactionProviderInterface)
                    transactionType = i.GetGenericArguments()[0];

            if (transactionType == null)
                throw new ConfigurationException(
                    $"Unable to register provider of type {transactionProvider.Name}. It does not implement {typeof(IAmABoxTransactionProvider<>).Name}.");

            //register the generic interface with the transaction type
            var boxProviderType = transactionProviderInterface.MakeGenericType(transactionType);

            brighterBuilder.Services.Add(new ServiceDescriptor(boxProviderType, transactionProvider, serviceLifetime));

            //NOTE: It is a little unsatisfactory to hard code our types in here
            RegisterRelationalProviderServicesMaybe(brighterBuilder, busConfiguration.ConnectionProvider,
                transactionProvider, serviceLifetime);
            RegisterDynamoProviderServicesMaybe(brighterBuilder, busConfiguration.ConnectionProvider,
                transactionProvider, serviceLifetime);
            
            //we always need an outbox in case of producer callbacks
            var outbox = busConfiguration.Outbox;
            if (outbox == null)
            {
                outbox = new InMemoryOutbox();
            }

            //we create the outbox from interfaces from the determined transaction type to prevent the need
            //to pass generic types as we know the transaction provider type
            var syncOutboxType = typeof(IAmAnOutboxSync<,>).MakeGenericType(typeof(Message), transactionType);
            var asyncOutboxType = typeof(IAmAnOutboxAsync<,>).MakeGenericType(typeof(Message), transactionType);

            foreach (Type i in outbox.GetType().GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == syncOutboxType)
                {
                    var outboxDescriptor =
                        new ServiceDescriptor(syncOutboxType, _ => outbox, ServiceLifetime.Singleton);
                    brighterBuilder.Services.Add(outboxDescriptor);
                }

                if (i.IsGenericType && i.GetGenericTypeDefinition() == asyncOutboxType)
                {
                    var asyncOutboxdescriptor =
                        new ServiceDescriptor(asyncOutboxType, _ => outbox, ServiceLifetime.Singleton);
                    brighterBuilder.Services.Add(asyncOutboxdescriptor);
                }
            }

            if (busConfiguration.UseRpc)
            {
                brighterBuilder.Services.TryAddSingleton<IUseRpc>(new UseRpc(busConfiguration.UseRpc, busConfiguration.ReplyQueueSubscriptions));
            }
            
            brighterBuilder.Services.TryAddSingleton<IAmExternalBusConfiguration>(busConfiguration);
           
            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAnExternalBusService),
               (serviceProvider) => BuildExternalBus(
                   serviceProvider, transactionType, busConfiguration, brighterBuilder.PolicyRegistry, outbox
                   ),
               ServiceLifetime.Singleton));

            return brighterBuilder;
        }
        
        private static INeedARequestContext AddEventBus(
            IServiceProvider provider,
            INeedMessaging messagingBuilder,
            IUseRpc useRequestResponse)
        {
            var eventBus = provider.GetService<IAmAnExternalBusService>();
            var eventBusConfiguration = provider.GetService<IAmExternalBusConfiguration>();
            var serviceActivatorOptions = provider.GetService<IServiceActivatorOptions>();

            INeedARequestContext ret = null;
            var hasEventBus = eventBus != null;
            bool useRpc = useRequestResponse != null && useRequestResponse.RPC;

            if (!hasEventBus) ret = messagingBuilder.NoExternalBus();

            if (hasEventBus && !useRpc)
            {
                ret = messagingBuilder.ExternalBus(
                    ExternalBusType.FireAndForget,
                    eventBus,
                    eventBusConfiguration.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions,
                    serviceActivatorOptions?.InboxConfiguration
                );
            }

            if (hasEventBus && useRpc)
            {
                ret = messagingBuilder.ExternalBus(
                    ExternalBusType.RPC,
                    eventBus,
                    eventBusConfiguration.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions,
                    serviceActivatorOptions?.InboxConfiguration
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
        
        private static IAmAnExternalBusService BuildExternalBus(IServiceProvider serviceProvider,
            Type transactionType,
            ExternalBusConfiguration busConfiguration,
            IPolicyRegistry<string> policyRegistry,
            IAmAnOutbox outbox) 
        {
            //Because the bus has specialized types as members, we need to create the bus type dynamically
            //again to prevent someone configuring Brighter from having to pass generic types
            var busType = typeof(ExternalBusService<,>).MakeGenericType(typeof(Message), transactionType);

            return (IAmAnExternalBusService)Activator.CreateInstance(busType,
                busConfiguration.ProducerRegistry,
                policyRegistry,
                busConfiguration.MessageMapperRegistry,
                TransformFactory(serviceProvider),
                TransformFactoryAsync(serviceProvider),
                outbox,
                busConfiguration.OutboxBulkChunkSize,
                busConfiguration.OutboxTimeout,
                busConfiguration.MaxOutStandingMessages,
                busConfiguration.MaxOutStandingCheckIntervalMilliSeconds,
                busConfiguration.OutBoxBag);
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

            var messageMapperRegistry = new MessageMapperRegistry(
                new ServiceProviderMapperFactory(provider),
                new ServiceProviderMapperFactoryAsync(provider)
            );

            foreach (var messageMapper in serviceCollectionMessageMapperRegistry.Mappers)
            {
                messageMapperRegistry.Register(messageMapper.Key, messageMapper.Value);
            }

            foreach (var messageMapper in serviceCollectionMessageMapperRegistry.AsyncMappers)
            {
                messageMapperRegistry.RegisterAsync(messageMapper.Key, messageMapper.Value);
            }

            return messageMapperRegistry;
        }

        private static void RegisterDynamoProviderServicesMaybe(
            IBrighterBuilder brighterBuilder,
            Type connectionProvider,
            Type transactionProvider,
            ServiceLifetime serviceLifetime)
        {
            //not all box transaction providers are also relational connection providers
            if (typeof(IAmADynamoDbConnectionProvider).IsAssignableFrom(connectionProvider))
            {
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmADynamoDbConnectionProvider),
                    connectionProvider, serviceLifetime));
            }

            //not all box transaction providers are also relational connection providers
            if (typeof(IAmADynamoDbTransactionProvider).IsAssignableFrom(transactionProvider))
            {
                //register the combined interface just in case
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmADynamoDbTransactionProvider),
                    transactionProvider, serviceLifetime));
            }
        }

        private static void RegisterRelationalProviderServicesMaybe(
            IBrighterBuilder brighterBuilder,
            Type connectionProvider,
            Type transactionProvider,
            ServiceLifetime serviceLifetime
        )
        {
            //not all box transaction providers are also relational connection providers
            if (typeof(IAmARelationalDbConnectionProvider).IsAssignableFrom(connectionProvider))
            {
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmARelationalDbConnectionProvider),
                    connectionProvider, serviceLifetime));
            }

            //not all box transaction providers are also relational connection providers
            if (typeof(IAmATransactionConnectionProvider).IsAssignableFrom(transactionProvider))
            {
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

        /// <summary>
        /// Creates transforms. Normally you don't need to call this, it is called by the builder for Brighter or
        /// the Service Activator
        /// Visibility is required for use from both
        /// </summary>
        /// <param name="provider">The IoC container to build the transform factory over</param>
        /// <returns></returns>
        public static ServiceProviderTransformerFactoryAsync TransformFactoryAsync(IServiceProvider provider)
        {
            return new ServiceProviderTransformerFactoryAsync(provider);
        }
    }
}
