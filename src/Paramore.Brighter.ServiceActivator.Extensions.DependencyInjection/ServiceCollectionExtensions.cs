using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

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
            services.TryAddSingleton<IBrighterOptions>(options);
            services.TryAddSingleton<IAmConsumerOptions>(options);
            
            services.TryAdd(new ServiceDescriptor(typeof(IDispatcher),
                BuildDispatcher,
                ServiceLifetime.Singleton));
            
            services.TryAddSingleton(options.InboxConfiguration);
            var inbox = options.InboxConfiguration.Inbox;
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
            
            return ServiceCollectionExtensions.BrighterHandlerBuilder(services, options);
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
            
            return dispatcherBuilder
                .MessageMappers(messageMapperRegistry, messageMapperRegistry, messageTransformFactory, messageTransformFactoryAsync)
                .ChannelFactory(options.DefaultChannelFactory ?? new InMemoryChannelFactory(new InternalBus(), TimeProvider.System) )
                .Subscriptions(options.Subscriptions)
                .ConfigureInstrumentation(tracer, options.InstrumentationOptions)
                .Build();
        }

        private static T BuildInbox<T>(IServiceProvider serviceProvider) where T : class, IAmAnInbox
        {
            var inboxConfig = serviceProvider.GetRequiredService<InboxConfiguration>();
            var tracer = serviceProvider.GetService<IAmABrighterTracer>();

            if (tracer != null)
            {
                inboxConfig.Inbox.Tracer = tracer;
            }

            return (inboxConfig.Inbox as T)!;
        }
    }
}
