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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;
using System.Text.Json;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;
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
            Action<BrighterOptions>? configure = null)
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
        ///  - Request Context Factory - how do we create a request context for a pipeline
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

            var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services, options.MapperLifetime);
            services.TryAddSingleton(mapperRegistry);
            
            services.TryAddSingleton(options.RequestContextFactory);

            if (options.FeatureSwitchRegistry != null)
                services.TryAddSingleton(options.FeatureSwitchRegistry);

            //Add the policy registry
#pragma warning disable CS0618 // Type or member is obsolete
            var policyRegistry = options.PolicyRegistry == null ? new DefaultPolicy() : AddDefaults(options.PolicyRegistry);
#pragma warning restore CS0618 // Type or member is obsolete

            services.TryAdd(new ServiceDescriptor(typeof(IAmACommandProcessor), BuildCommandProcessor, options.CommandProcessorLifetime));

            var builder =  new ServiceCollectionBrighterBuilder(
                services,
                subscriberRegistry,
                mapperRegistry,
                transformRegistry,
                policyRegistry
            );

            return builder
                .UseScheduler(new InMemorySchedulerFactory())
                .UseExternalLuggageStore<NullLuggageStore>();
        }

        /// <summary>
        ///We use AddProducers to register an external bus, which is a bus that is not the Brighter In-Memory Bus.
        /// The external bus uses Message Oriented Middleware (MoM) to dispatch a message from a producer
        /// to a consumer. 
        /// Registers singletons with the service collection :-
        ///     -- An Outbox Producer Mediatory - used to send message externally via an Outbox:
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
                throw new ArgumentNullException($"{nameof(brighterBuilder)} cannot be null.", nameof(brighterBuilder));

            var busConfiguration = new ProducersConfiguration();
            configure?.Invoke(busConfiguration);

            if (busConfiguration.ProducerRegistry == null)
                throw new ConfigurationException("An external bus must have an IAmAProducerRegistry");

            if (busConfiguration.UseRpc && busConfiguration.ReplyQueueSubscriptions == null)
                throw new ConfigurationException("If the you configure RPC, you must configure the ReplyQueueSubscriptions");
            
            brighterBuilder.Services.TryAddSingleton<IAmAPublicationFinder, FindPublicationByPublicationTopicOrRequestType >();
            brighterBuilder.Services.TryAddSingleton(busConfiguration.ProducerRegistry);

            //default to using System Transactions if nothing provided, so we always technically can share the outbox transaction
            Type transactionProvider = busConfiguration.TransactionProvider ?? typeof(CommittableTransactionProvider);

            //Find the transaction type from the provider
            Type transactionProviderInterface = typeof(IAmABoxTransactionProvider<>);
            Type? transactionType = null;
            foreach (Type i in transactionProvider.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == transactionProviderInterface)
                {
                    transactionType = i.GetGenericArguments()[0];
                }
            }

            if (transactionType == null)
                throw new ConfigurationException(
                    $"Unable to register provider of type {transactionProvider.Name}. It does not implement {typeof(IAmABoxTransactionProvider<>).Name}.");

            //register the generic interface with the transaction type
            var boxProviderType = transactionProviderInterface.MakeGenericType(transactionType);

            // Register the transaction provider against both the generic and non-generic interface. The non-generic interface is needed by the CommandProcessor
            brighterBuilder.Services.Add(new ServiceDescriptor(boxProviderType, transactionProvider, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionProvider), transactionProvider, serviceLifetime));

            if (busConfiguration.ConnectionProvider != null)
                RegisterConnectionAndTransactionProvider(brighterBuilder, busConfiguration.ConnectionProvider, transactionProvider, serviceLifetime);
            
            //we always need an outbox in case of producer callbacks
            var outbox = busConfiguration.Outbox ?? new InMemoryOutbox(TimeProvider.System);

            //we create the outbox from interfaces from the determined transaction type to prevent the need
            //to pass generic types as we know the transaction provider type
            var syncOutboxType = typeof(IAmAnOutboxSync<,>).MakeGenericType(typeof(Message), transactionType);
            var asyncOutboxType = typeof(IAmAnOutboxAsync<,>).MakeGenericType(typeof(Message), transactionType);

            var outboxInterfaces = outbox.GetType().GetInterfaces();
            var syncOutboxInterface = outboxInterfaces
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == syncOutboxType);
            var asyncOutboxInterface = outboxInterfaces
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == asyncOutboxType);

            if (syncOutboxInterface == null && asyncOutboxType == null)
            {
                throw new ConfigurationException(
                    $"Unable to register outbox of type {outbox.GetType().Name} - no transaction provider has been registered that matches the outbox's transaction type");
            }

            if (syncOutboxInterface != null)
            {
                var outboxDescriptor =
                        new ServiceDescriptor(syncOutboxType, _ => outbox, ServiceLifetime.Singleton);
                brighterBuilder.Services.Add(outboxDescriptor);
            }

            if (asyncOutboxInterface != null)
            {
                var asyncOutboxdescriptor =
                        new ServiceDescriptor(asyncOutboxType, _ => outbox, ServiceLifetime.Singleton);
                brighterBuilder.Services.Add(asyncOutboxdescriptor);
            }
            
            // If no distributed locking service is added, then add the in memory variant
            var distributedLock = busConfiguration.DistributedLock ?? new InMemoryLock();
            brighterBuilder.Services.AddSingleton(distributedLock);

            if (busConfiguration.UseRpc)
                brighterBuilder.Services.TryAddSingleton<IUseRpc>(new UseRpc(busConfiguration.UseRpc, busConfiguration.ReplyQueueSubscriptions!));
            
            brighterBuilder.Services.TryAddSingleton<IAmProducersConfiguration>(busConfiguration);
            brighterBuilder.ResiliencePolicyRegistry ??= new ResiliencePipelineRegistry<string>().AddBrighterDefault();
           
            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAnOutboxProducerMediator),
               (serviceProvider) => BuildOutBoxProducerMediator(
                   serviceProvider, transactionType, busConfiguration, brighterBuilder.ResiliencePolicyRegistry, outbox
               ) ?? throw new ConfigurationException("Unable to create an outbox producer mediator; are you missing a registration?"),
               ServiceLifetime.Singleton));

            return brighterBuilder;
        }

        /// <summary>
        /// Set a default <see cref="IAmAPublicationFinder"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="lifetime"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IBrighterBuilder UsePublicationFinder<T>(this IBrighterBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Transient)
            where T : class, IAmAPublicationFinder
        {
            builder.Services.Add(new ServiceDescriptor(typeof(IAmAPublicationFinder), typeof(T), lifetime));
            return builder;
        }
        
        /// <summary>
        /// Set a default <see cref="IAmAPublicationFinder"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="instance"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IBrighterBuilder UsePublicationFinder<T>(this IBrighterBuilder builder, T instance)
            where T : class, IAmAPublicationFinder
        {
            builder.Services.AddSingleton<IAmAPublicationFinder>(instance);
            return builder;
        }
        
         /// <summary>
         /// An external request scheduler factory
         /// </summary>
         /// <param name="builder">The builder.</param>
         /// <param name="factory">The message scheduler factory</param>
         /// <returns></returns>
         public static IBrighterBuilder UseScheduler<T>(this IBrighterBuilder builder, T factory)
            where T : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
         { 
             builder
                 .UseRequestScheduler(factory)
                 .UseMessageScheduler(factory);
             return builder;
         }
         
         /// <summary>
         /// An external request scheduler factory
         /// </summary>
         /// <param name="builder">The builder.</param>
         /// <param name="factory">The message scheduler factory</param>
         /// <returns></returns>
         public static IBrighterBuilder UseScheduler<T>(this IBrighterBuilder builder, Func<IServiceProvider, T> factory)
            where T : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
         {
             builder
                 .UseRequestScheduler(provider => factory(provider))
                 .UseMessageScheduler(provider => factory(provider));
             return builder;
         }
        
        /// <summary>
        /// An external request scheduler factory
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="factory">The message scheduler factory</param>
        /// <returns></returns>
        public static IBrighterBuilder UseRequestScheduler(this IBrighterBuilder builder, IAmARequestSchedulerFactory factory)
        {
            builder.Services.AddSingleton(factory);
            builder.Services.TryAddSingleton(provide =>
            {
                var command = provide.GetRequiredService<IAmACommandProcessor>();
                var schedulerfactory = provide.GetRequiredService<IAmARequestSchedulerFactory>();
                return schedulerfactory.CreateSync(command);
            });
            builder.Services.TryAddSingleton(provide =>
            {
                var command = provide.GetRequiredService<IAmACommandProcessor>();
                var schedulerFactory = provide.GetRequiredService<IAmARequestSchedulerFactory>();
                return schedulerFactory.CreateAsync(command);
            });
            return builder;
        }
        
        /// <summary>
        /// An external request scheduler factory
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="factory">The message scheduler factory</param>
        /// <returns></returns>
        public static IBrighterBuilder UseRequestScheduler(this IBrighterBuilder builder, Func<IServiceProvider, IAmAMessageSchedulerFactory> factory)
        {
            builder.Services.AddSingleton(factory);
            return builder;
        }
        
        /// <summary>
        /// An external message scheduler factory
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="factory">The message scheduler factory</param>
        /// <returns></returns>
        public static IBrighterBuilder UseMessageScheduler(this IBrighterBuilder builder, IAmAMessageSchedulerFactory factory)
        {
            builder.Services.AddSingleton(factory);
            builder.Services.TryAddSingleton(provider =>
            {
                var messageSchedulerFactory = provider.GetRequiredService<IAmAMessageSchedulerFactory>();
                var processor = provider.GetRequiredService<IAmACommandProcessor>();
                return messageSchedulerFactory.Create(processor);
            });
            builder.Services.TryAddSingleton(provide => (IAmAMessageSchedulerAsync)provide.GetRequiredService<IAmAMessageScheduler>());
            builder.Services.TryAddSingleton(provide => (IAmAMessageSchedulerSync)provide.GetRequiredService<IAmAMessageScheduler>());
            return builder;
        }
        
        /// <summary>
        /// An external message scheduler factory
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="factory">The message scheduler factory</param>
        /// <returns></returns>
        public static IBrighterBuilder UseMessageScheduler(this IBrighterBuilder builder, Func<IServiceProvider, IAmAMessageSchedulerFactory> factory)
        {
            builder.Services.AddSingleton(factory);
            return builder;
        }
        
        private static INeedInstrumentation AddEventBus(
            IServiceProvider provider,
            INeedMessaging messagingBuilder,
            IUseRpc? useRequestResponse)
        {
            var eventBus = provider.GetService<IAmAnOutboxProducerMediator>();
            var hasEventBus = eventBus != null;
            
            var eventBusConfiguration = provider.GetService<IAmProducersConfiguration>();
            var transactionProvider = provider.GetService<IAmABoxTransactionProvider>();
            var serviceActivatorOptions = provider.GetService<IAmConsumerOptions>();

            INeedInstrumentation? instrumentationBuilder = null;
            bool useRpc = useRequestResponse != null && useRequestResponse.RPC;

            if (!hasEventBus) instrumentationBuilder = messagingBuilder.NoExternalBus();

            if (hasEventBus && !useRpc)
            {
                instrumentationBuilder = messagingBuilder.ExternalBus(
                    ExternalBusType.FireAndForget,
                    eventBus!,
                    transactionProvider,
                    eventBusConfiguration!.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions,
                    serviceActivatorOptions?.InboxConfiguration);
            }

            if (hasEventBus && useRpc)
            {
                instrumentationBuilder = messagingBuilder.ExternalBus(
                    ExternalBusType.RPC,
                    eventBus!,
                    transactionProvider,
                    eventBusConfiguration!.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions,
                    serviceActivatorOptions?.InboxConfiguration);
            }

            return instrumentationBuilder!;
        }

        private static IPolicyRegistry<string>? AddDefaults(IPolicyRegistry<string>? policyRegistry)
        {
            if (policyRegistry == null)
                throw new ConfigurationException("You must add a policy registry, to which defaults can be added");
            
#pragma warning disable CS0618 // Type or member is obsolete
            if (!policyRegistry.ContainsKey(CommandProcessor.RETRYPOLICY))
                throw new ConfigurationException(
                    "The policy registry is missing the CommandProcessor.RETRYPOLICY policy which is required");

            if (!policyRegistry.ContainsKey(CommandProcessor.CIRCUITBREAKER))
                throw new ConfigurationException(
                    "The policy registry is missing the CommandProcessor.CIRCUITBREAKER policy which is required");
#pragma warning restore CS0618 // Type or member is obsolete

            return policyRegistry;
        }

        private static IAmACommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var loggerFactory = provider.GetService<ILoggerFactory>();
            //if not supplied, use the default logger factory, which has no providers
            if (loggerFactory != null)
                ApplicationLogging.LoggerFactory = loggerFactory;


            var options = provider.GetRequiredService<IBrighterOptions>();
            var subscriberRegistry = provider.GetRequiredService<ServiceCollectionSubscriberRegistry>();
            var useRequestResponse = provider.GetService<IUseRpc>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory);

            var handlerBuilder = CommandProcessorBuilder.StartNew();

            var featureSwitchRegistry = provider.GetService<IAmAFeatureSwitchRegistry>();

            if (featureSwitchRegistry != null)
                handlerBuilder = handlerBuilder.ConfigureFeatureSwitches(featureSwitchRegistry);
            
            var pollyBuilder = handlerBuilder.Handlers(handlerConfiguration);

            options.ResiliencePipelineRegistry ??= new ResiliencePipelineRegistry<string>().AddBrighterDefault();
#pragma warning disable CS0618 // Type or member is obsolete
            var messagingBuilder = pollyBuilder.Resilience(options.ResiliencePipelineRegistry, options.PolicyRegistry);
#pragma warning restore CS0618 // Type or member is obsolete

            
            var command = AddEventBus(provider, messagingBuilder, useRequestResponse)
                .ConfigureInstrumentation(provider.GetService<IAmABrighterTracer>(), options.InstrumentationOptions)
                .RequestContextFactory(provider.GetRequiredService<IAmARequestContextFactory>())
                .RequestSchedulerFactory(provider.GetRequiredService<IAmARequestSchedulerFactory>())
                .Build();
            
            var eventBusConfiguration = provider.GetService<IAmProducersConfiguration>();
            var producerRegistry = provider.GetService<IAmAProducerRegistry>();
            var messageSchedulerFactory = eventBusConfiguration?.MessageSchedulerFactory ?? provider.GetRequiredService<IAmAMessageSchedulerFactory>();
            producerRegistry?.Producers
                .Each(x => x.Scheduler ??= messageSchedulerFactory.Create(command));

            return command;
        }
        
        private static IAmAnOutboxProducerMediator? BuildOutBoxProducerMediator(IServiceProvider serviceProvider,
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
                MessageMapperRegistry(serviceProvider),
                TransformFactory(serviceProvider),
                TransformFactoryAsync(serviceProvider),
                Tracer(serviceProvider),
                PublicationFinder(serviceProvider),
                outbox,
                OutboxCircuitBreaker(serviceProvider),
                RequestContextFactory(serviceProvider),
                busConfiguration.OutboxTimeout,
                busConfiguration.MaxOutStandingMessages,
                busConfiguration.MaxOutStandingCheckInterval,
                busConfiguration.OutBoxBag,
                TimeProvider.System,
                busConfiguration.InstrumentationOptions);
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
            var serviceCollectionMessageMapperRegistry = provider.GetRequiredService<ServiceCollectionMessageMapperRegistryBuilder>();

            var messageMapperRegistry = new MessageMapperRegistry(
                new ServiceProviderMapperFactory(provider),
                new ServiceProviderMapperFactoryAsync(provider),
                serviceCollectionMessageMapperRegistry.DefaultMessageMapper,
                serviceCollectionMessageMapperRegistry.DefaultMessageMapperAsync
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

        private static void RegisterConnectionAndTransactionProvider(IBrighterBuilder brighterBuilder, 
            Type connectionProvider,
            Type transactionProvider,
            ServiceLifetime serviceLifetime)
        {
            var connectionProviderInterface = GetConnectionProviderInterface(connectionProvider);
            if(connectionProviderInterface != null)
            {
                brighterBuilder.Services.TryAdd(new ServiceDescriptor(connectionProviderInterface, connectionProvider, serviceLifetime));
                
                var transactionProviderInterface = GetTransactionInterface(transactionProvider, connectionProviderInterface );
                if(transactionProviderInterface != null)
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

        /// <summary>
        /// Grabs the Request Context Factory from DI. Mainly used to create a similar level of
        /// abstraction to the other providers for building an external service bus
        /// </summary>
        /// <param name="provider"></param>
        public static IAmARequestContextFactory RequestContextFactory(IServiceProvider provider)
        {
            return provider.GetRequiredService<IAmARequestContextFactory>();
        }

        public static IAmAPublicationFinder PublicationFinder(IServiceProvider provider)
        {
            return provider.GetRequiredService<IAmAPublicationFinder>();
        }
        
        private static IAmABrighterTracer? Tracer(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<BrighterTracer>();
        }
        private static IAmAnOutboxCircuitBreaker? OutboxCircuitBreaker(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IAmAnOutboxCircuitBreaker>();
        }

        /// <summary>                                                            x
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
        
        /// <summary>
        /// Adds a singleton instance of an external luggage (claim check) store provider to the Brighter framework.
        /// This method is used when you have a pre-initialized instance of your storage provider.
        /// The store provider must implement both <see cref="IAmAStorageProvider"/> for synchronous operations
        /// and <see cref="IAmAStorageProviderAsync"/> for asynchronous operations.
        /// </summary>
        /// <typeparam name="TStoreProvider">The concrete type of the storage provider.
        /// Must implement <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/>.</typeparam>
        /// <param name="builder">The <see cref="IBrighterBuilder"/> instance to which the storage provider will be added.</param>
        /// <returns>The <see cref="IBrighterBuilder"/> instance for chaining.</returns>
        public static IBrighterBuilder UseExternalLuggageStore<TStoreProvider>(this IBrighterBuilder builder)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            builder.Services.AddSingleton<TStoreProvider>()
                .RegisterLuggageStore<TStoreProvider>();

            return builder;
        }
        
        /// <summary>
        /// Adds a singleton instance of an external luggage (claim check) store provider to the Brighter framework.
        /// This method is used when you have a pre-initialized instance of your storage provider.
        /// The store provider must implement both <see cref="IAmAStorageProvider"/> for synchronous operations
        /// and <see cref="IAmAStorageProviderAsync"/> for asynchronous operations.
        /// </summary>
        /// <typeparam name="TStoreProvider">The concrete type of the storage provider.
        /// Must implement <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/>.</typeparam>
        /// <param name="builder">The <see cref="IBrighterBuilder"/> instance to which the storage provider will be added.</param>
        /// <param name="storeProvider">The pre-initialized instance of the storage provider.</param>
        /// <returns>The <see cref="IBrighterBuilder"/> instance for chaining.</returns>
        public static IBrighterBuilder UseExternalLuggageStore<TStoreProvider>(this IBrighterBuilder builder, TStoreProvider storeProvider)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            builder.Services.AddSingleton(storeProvider)
                 .RegisterLuggageStore<TStoreProvider>();

            return builder;
        }
        
        /// <summary>
        /// Adds a singleton instance of a luggage (claim check) store provider to the Brighter framework,
        /// resolved via a factory function. This method is used when the storage provider
        /// needs to be instantiated by the service provider (e.g., to inject its own dependencies).
        /// The store provider must implement both <see cref="IAmAStorageProvider"/> for synchronous operations
        /// and <see cref="IAmAStorageProviderAsync"/> for asynchronous operations.
        /// </summary>
        /// <typeparam name="TStoreProvider">The concrete type of the storage provider.
        /// Must implement <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/>.</typeparam>
        /// <param name="builder">The <see cref="IBrighterBuilder"/> instance to which the storage provider will be added.</param>
        /// <param name="storeProvider">A factory function that takes an <see cref="IServiceProvider"/> and returns an instance of the storage provider.</param>
        /// <returns>The <see cref="IBrighterBuilder"/> instance for chaining.</returns>
        public static IBrighterBuilder UseExternalLuggageStore<TStoreProvider>(this IBrighterBuilder builder, Func<IServiceProvider, TStoreProvider> storeProvider)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            builder.Services.AddSingleton(storeProvider)
                .RegisterLuggageStore<TStoreProvider>();
            
            return builder;
        }

        private static void RegisterLuggageStore<TStoreProvider>(this IServiceCollection services)
            where TStoreProvider : class, IAmAStorageProvider, IAmAStorageProviderAsync
        {
            services
                .AddSingleton(provider =>
                {
                    IAmAStorageProvider store = provider.GetRequiredService<TStoreProvider>();
                    store.Tracer = provider.GetRequiredService<IAmABrighterTracer>();
                    store.EnsureStoreExists();
                    return store;
                })
                .AddSingleton(provider =>
                {
                    IAmAStorageProviderAsync store = provider.GetRequiredService<TStoreProvider>();
                    store.Tracer = provider.GetRequiredService<IAmABrighterTracer>();
                    store.EnsureStoreExistsAsync().GetAwaiter().GetResult();
                    return store;
                });
        }
    }
}
