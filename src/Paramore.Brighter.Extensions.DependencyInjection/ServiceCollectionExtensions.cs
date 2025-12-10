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
    /// <summary>
    /// Extension methods for adding Brighter to the .NET IoC Container - ServiceCollection.
    /// This class contains the core setup methods for Brighter. Additional configuration methods
    /// are available in separate extension classes:
    /// - <see cref="ProducersServiceCollectionExtensions"/> for producer/external bus configuration
    /// - <see cref="SchedulerServiceCollectionExtensions"/> for scheduler configuration
    /// - <see cref="LuggageStoreServiceCollectionExtensions"/> for luggage store configuration
    /// </summary>
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

            return BrighterHandlerBuilder(services, options);
        }

        /// <summary>
        /// Will add Brighter into the .NET IoC Container - ServiceCollection with access to IServiceProvider
        /// for resolving dependencies during configuration.
        /// Registers the following with the service collection :-
        ///  - BrighterOptions - how should we configure Brighter
        ///  - Feature Switch Registry - optional if features switch support is desired
        ///  - Inbox - defaults to InMemoryInbox if none supplied
        ///  - SubscriberRegistry - what handlers subscribe to what requests
        ///  - MapperRegistry - what mappers translate what messages
        /// </summary>
        /// <param name="services">The collection of services that we want to add registrations to</param>
        /// <param name="configure">A callback that defines what options to set when Brighter is built, with access to IServiceProvider</param>
        /// <returns>A builder that can be used to populate the IoC container with handlers and mappers by inspection
        /// - used by built in factory from CommandProcessor</returns>
        /// <exception cref="ArgumentNullException">Thrown if we have no IoC provided ServiceCollection</exception>
        public static IBrighterBuilder AddBrighter(
            this IServiceCollection services,
            Action<BrighterOptions, IServiceProvider>? configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<BrighterOptions>(sp =>
            {
                var options = new BrighterOptions();
                configure?.Invoke(options, sp);
                return options;
            });

            services.TryAddSingleton<IBrighterOptions>(sp => sp.GetRequiredService<BrighterOptions>());

            return BrighterHandlerBuilder(services, new BrighterOptions());
        }

        /// <summary>
        /// This is public so that we can call it from ServiceActivator extensions.
        /// DON'T CALL THIS DIRECTLY - use AddBrighter or AddConsumers instead.
        /// </summary>
        /// <param name="services">The collection of services that we want to add registrations to</param>
        /// <param name="options">The options containing lifetime configurations for registries</param>
        /// <returns>A builder for further configuration</returns>
        public static IBrighterBuilder BrighterHandlerBuilder(IServiceCollection services, BrighterOptions options)
        {
            services.TryAddSingleton<IBrighterOptions>(options);

            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services, options.HandlerLifetime);
            services.TryAddSingleton(subscriberRegistry);

            var transformRegistry = new ServiceCollectionTransformerRegistry(services, options.TransformerLifetime);
            services.TryAddSingleton(transformRegistry);

            var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services, options.MapperLifetime);
            services.TryAddSingleton(mapperRegistry);

            services.TryAddSingleton<IAmARequestContextFactory>(sp =>
                sp.GetRequiredService<IBrighterOptions>().RequestContextFactory);

            if (options.FeatureSwitchRegistry != null)
                services.TryAddSingleton(options.FeatureSwitchRegistry);

#pragma warning disable CS0618 // Type or member is obsolete
            var policyRegistry = options.PolicyRegistry == null ? new DefaultPolicy() : AddDefaults(options.PolicyRegistry);
#pragma warning restore CS0618 // Type or member is obsolete

            services.TryAdd(new ServiceDescriptor(typeof(IAmACommandProcessor), BuildCommandProcessor, ServiceLifetime.Singleton));

            var builder = new ServiceCollectionBrighterBuilder(
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

        /// <summary>
        /// Grabs the Request Context Factory from DI. Mainly used to create a similar level of
        /// abstraction to the other providers for building an external service bus
        /// </summary>
        /// <param name="provider">The service provider</param>
        /// <returns>The request context factory</returns>
        public static IAmARequestContextFactory RequestContextFactory(IServiceProvider provider)
        {
            return provider.GetRequiredService<IAmARequestContextFactory>();
        }

        /// <summary>
        /// Gets the publication finder from DI.
        /// </summary>
        /// <param name="provider">The service provider</param>
        /// <returns>The publication finder</returns>
        public static IAmAPublicationFinder PublicationFinder(IServiceProvider provider)
        {
            return provider.GetRequiredService<IAmAPublicationFinder>();
        }

        /// <summary>
        /// Creates transforms. Normally you don't need to call this, it is called by the builder for Brighter or
        /// the Service Activator
        /// Visibility is required for use from both
        /// </summary>
        /// <param name="provider">The IoC container to build the transform factory over</param>
        /// <returns>The transform factory</returns>
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
        /// <returns>The async transform factory</returns>
        public static ServiceProviderTransformerFactoryAsync TransformFactoryAsync(IServiceProvider provider)
        {
            return new ServiceProviderTransformerFactoryAsync(provider);
        }

        /// <summary>
        /// Gets the tracer from DI if available.
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <returns>The tracer or null if not registered</returns>
        internal static IAmABrighterTracer? Tracer(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<BrighterTracer>();
        }

        /// <summary>
        /// Gets the outbox circuit breaker from DI if available.
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <returns>The circuit breaker or null if not registered</returns>
        internal static IAmAnOutboxCircuitBreaker? OutboxCircuitBreaker(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IAmAnOutboxCircuitBreaker>();
        }

        private static INeedInstrumentation AddEventBus(
            IServiceProvider provider,
            INeedMessaging messagingBuilder,
            IUseRpc? useRequestResponse)
        {
            var eventBus = provider.GetService<IAmAnOutboxProducerMediator>();
            var hasEventBus = eventBus != null;

            var eventBusConfiguration = provider.GetService<IAmProducersConfiguration>();
            var serviceActivatorOptions = provider.GetService<IAmConsumerOptions>();

            //The transaction provider is often scoped, such as when we have a DbContext. We only need to pull
            //the transaction type, so we create a scope to get the provider, then pull the type from it
            Type? transactionType = null;
            using (IServiceScope serviceScope = provider.CreateScope())
            {
                var transactionProvider = serviceScope.ServiceProvider.GetService<IAmABoxTransactionProvider>();
                transactionType = CommandProcessor.GetTransactionTypeFromTransactionProvider(transactionProvider);
            }

            INeedInstrumentation? instrumentationBuilder = null;
            bool useRpc = useRequestResponse != null && useRequestResponse.RPC;

            if (!hasEventBus) instrumentationBuilder = messagingBuilder.NoExternalBus();

            if (hasEventBus && !useRpc)
            {
                instrumentationBuilder = messagingBuilder.ExternalBus(
                    ExternalBusType.FireAndForget,
                    eventBus!,
                    transactionType,
                    eventBusConfiguration!.ResponseChannelFactory,
                    eventBusConfiguration.ReplyQueueSubscriptions,
                    serviceActivatorOptions?.InboxConfiguration);
            }

            if (hasEventBus && useRpc)
            {
                instrumentationBuilder = messagingBuilder.ExternalBus(
                    ExternalBusType.RPC,
                    eventBus!,
                    transactionType,
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
    }
}
