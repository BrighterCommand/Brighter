using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Validation;
using Paramore.Brighter.Validation;
using Microsoft.Extensions.Options;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding a service activator to the .NET IoC container
    /// </summary>
    public static class  ServiceActivatorServiceCollectionExtensions 
    {
        /// <summary>
        /// Adds a service activator to the .NET IoC Container, used to register one or more message pump for a subscription to messages on an external bus
        /// Registers as a singleton :-
        /// - Brighter Options - how we are configuring the command processor used for dispatch of message to handler
        /// - Dispatcher - the supervisor for the subscription workers
        /// </summary>
        /// <param name="services">The .NET IoC container to register with</param>
        /// <param name="configure">The configuration of the subscriptions</param>
        /// <returns>A brighter handler builder, used for chaining</returns>
        /// <exception cref="ArgumentNullException">Throws if no .NET IoC container provided</exception>
        public static IBrighterBuilder AddConsumers(
            this IServiceCollection services,
            Action<ConsumersOptions>? configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            var options = new ConsumersOptions();
            configure?.Invoke(options);
            services.TryAddSingleton<IAmConsumerOptions>(options);
            
            services.TryAdd(new ServiceDescriptor(typeof(IDispatcher),
                BuildDispatcher,
                ServiceLifetime.Singleton));
            
            if (options.InboxConfiguration != null)
                services.TryAddSingleton(options.InboxConfiguration);

            var inbox = options.InboxConfiguration?.Inbox;
            if (inbox is IAmAnInboxSync)
            {
                services.TryAdd(
                    new ServiceDescriptor(
                        typeof(IAmAnInboxSync), BuildInbox<IAmAnInboxSync>, ServiceLifetime.Singleton));
            }
            if (inbox is IAmAnInboxAsync)
            {
                services.TryAdd(
                    new ServiceDescriptor(
                        typeof(IAmAnInboxAsync), BuildInbox<IAmAnInboxAsync>, ServiceLifetime.Singleton));
            }
            
            services.Configure<BrighterPipelineValidationOptions>(o => o.ConsumerOwnsValidation = true);

            RegisterConsumerValidationSpecs(services);

            return ServiceCollectionExtensions.BrighterHandlerBuilder(services, options);
        }

        /// <summary>
        /// Adds a service activator to the .NET IoC Container, used to register one or more message pump for a subscription to messages on an external bus
        /// This overload accepts a Func that receives IServiceProvider, allowing dependencies to be resolved during configuration
        /// Registers as a singleton :-
        /// - Brighter Options - how we are configuring the command processor used for dispatch of message to handler
        /// - Dispatcher - the supervisor for the subscription workers
        /// </summary>
        /// <param name="services">The .NET IoC container to register with</param>
        /// <param name="configure">The configuration function that receives IServiceProvider and returns ConsumersOptions</param>
        /// <returns>A brighter handler builder, used for chaining</returns>
        /// <remarks>
        /// Inbox services are resolved lazily. Resolving an inbox without an
        /// <see cref="InboxConfiguration"/>, or with an inbox that does not implement the requested
        /// sync or async interface, throws <see cref="ConfigurationException"/>.
        /// Handlers without inbox middleware do not require an inbox configuration.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Throws if no .NET IoC container or configure function provided</exception>
        public static IBrighterBuilder AddConsumers(
            this IServiceCollection services,
            Func<IServiceProvider, ConsumersOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            // Register options with deferred resolution - ensure both interfaces resolve to the same instance
            services.TryAddSingleton<IAmConsumerOptions>(sp =>
                (IAmConsumerOptions)sp.GetRequiredService<IBrighterOptions>());

            // Defer dispatcher registration to use resolved options
            services.TryAdd(new ServiceDescriptor(typeof(IDispatcher),
                BuildDispatcher,
                ServiceLifetime.Singleton));

            // Defer inbox configuration registration
            services.TryAddSingleton<InboxConfiguration>(sp =>
            {
                var options = sp.GetRequiredService<IAmConsumerOptions>();
                return options.InboxConfiguration
                    ?? throw new ConfigurationException(
                        "InboxConfiguration is required to resolve an inbox. " +
                        "Set ConsumersOptions.InboxConfiguration in AddConsumers.");
            });

            // Register inbox implementations with deferred resolution
            services.TryAddSingleton<IAmAnInboxSync>(BuildInbox<IAmAnInboxSync>);
            services.TryAddSingleton<IAmAnInboxAsync>(BuildInbox<IAmAnInboxAsync>);

            services.Configure<BrighterPipelineValidationOptions>(o => o.ConsumerOwnsValidation = true);

            RegisterConsumerValidationSpecs(services);

            return ServiceCollectionExtensions.BrighterHandlerBuilder(
                services,
                sp => configure(sp));
        }

        private static Dispatcher BuildDispatcher(IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            //if not supplied, use the default logger factory, which has no providers
            if (loggerFactory != null)
                ApplicationLogging.LoggerFactory = loggerFactory;
        
            var options = serviceProvider.GetRequiredService<IAmConsumerOptions>();
            
            var commandProcessor = serviceProvider.GetRequiredService<IAmACommandProcessor>();
        
            var requestContextFactory = serviceProvider.GetService<IAmARequestContextFactory>() ?? new InMemoryRequestContextFactory();
            
            var dispatcherBuilder = DispatchBuilder
                .StartNew()
                .CommandProcessor(commandProcessor, requestContextFactory);
        
            var messageMapperRegistry = ServiceCollectionExtensions.MessageMapperRegistry(serviceProvider);
            var messageTransformFactory = ServiceCollectionExtensions.TransformFactory(serviceProvider);
            var messageTransformFactoryAsync = ServiceCollectionExtensions.TransformFactoryAsync(serviceProvider);
            
            var tracer = serviceProvider.GetService<IAmABrighterTracer>();

            var channelFactory = options.DefaultChannelFactory ?? new InMemoryChannelFactory(new InternalBus(), TimeProvider.System);
            var scheduler = serviceProvider.GetService<IAmAMessageScheduler>();
            if (channelFactory is IAmAChannelFactoryWithScheduler schedulerAwareFactory)
            {
                schedulerAwareFactory.Scheduler = scheduler;
            }

            // Wire scheduler on per-subscription channel factories that differ from the default
            foreach (var subscription in options.Subscriptions)
            {
                if (subscription.ChannelFactory is IAmAChannelFactoryWithScheduler subFactory
                    && !ReferenceEquals(subFactory, channelFactory))
                {
                    subFactory.Scheduler = scheduler;
                }
            }

            //the registry and transform factories are newed up above solely for this Dispatcher and are not
            //container-registered, so the Dispatcher is their sole owner and disposes them at container teardown
            return dispatcherBuilder
                .MessageMappers(messageMapperRegistry, messageMapperRegistry, messageTransformFactory, messageTransformFactoryAsync)
                .ChannelFactory(channelFactory)
                .Subscriptions(options.Subscriptions)
                .ConfigureInstrumentation(tracer, options.InstrumentationOptions)
                .Build(ownsRegistry: true, ownsTransformerFactories: true, shutdownTimeout: options.ShutdownTimeout);
        }

        private static T BuildInbox<T>(IServiceProvider serviceProvider) where T : class, IAmAnInbox
        {
            var inboxConfig = serviceProvider.GetRequiredService<InboxConfiguration>();
            if (inboxConfig.Inbox is not T inbox)
                throw new ConfigurationException(
                    $"InboxConfiguration.Inbox must implement {typeof(T).Name}, " +
                    $"but the configured inbox is '{inboxConfig.Inbox.GetType().Name}'.");

            var tracer = serviceProvider.GetService<IAmABrighterTracer>();

            if (tracer != null)
            {
                inboxConfig.Inbox.Tracer = tracer;
            }

            return inbox;
        }

        private static void RegisterConsumerValidationSpecs(IServiceCollection services)
        {
            services.AddSingleton<ISpecification<Subscription>>(sp =>
            {
                var inspector = sp.GetService<IAmASubscriberRegistryInspector>()
                    ?? (IAmASubscriberRegistryInspector)sp.GetRequiredService<ServiceCollectionSubscriberRegistry>();
                return ConsumerValidationRules.PumpHandlerMatch(inspector);
            });
            services.AddSingleton<ISpecification<Subscription>>(sp =>
            {
                var inspector = sp.GetService<IAmASubscriberRegistryInspector>()
                    ?? (IAmASubscriberRegistryInspector)sp.GetRequiredService<ServiceCollectionSubscriberRegistry>();
                return ConsumerValidationRules.HandlerRegistered(inspector);
            });
            services.AddSingleton<ISpecification<Subscription>>(_ =>
                ConsumerValidationRules.RequestTypeSubtype());
            services.AddSingleton<ISpecification<Subscription>>(_ =>
                ConsumerValidationRules.ZeroBudget());
            services.AddSingleton<ISpecification<Subscription>>(_ =>
                ConsumerValidationRules.BudgetAtNativeRedriveLimit());
            services.AddSingleton<ISpecification<Subscription>>(_ =>
                ConsumerValidationRules.UnenforceableBudget());
            services.AddSingleton<ISpecification<Subscription>>(sp =>
            {
                // The transformer-resolvability probe is registered by ValidatePipelines (a complete snapshot
                // of the service collection). When it — or the mapper registry — is absent (e.g. AddConsumers
                // without ValidatePipelines), the check is inert; validation only runs when ValidatePipelines
                // is enabled, so the inert specification is never evaluated in that case.
                var probe = sp.GetService<IAmATransformerResolvabilityProbe>();
                var mapperRegistryBuilder = sp.GetService<ServiceCollectionMessageMapperRegistryBuilder>();
                if (probe is null || mapperRegistryBuilder is null)
                    return new Specification<Subscription>(_ => []);

                return ConsumerValidationRules.UnwrapTransformResolvable(
                    () => ServiceCollectionExtensions.MessageMapperRegistry(sp), probe);
            });
            services.AddSingleton<ISpecification<Subscription>>(sp =>
                ConsumerValidationRules.ChannelFactoryCompatible(sp.GetService<IAmConsumerOptions>()?.DefaultChannelFactory));
        }
    }
}
