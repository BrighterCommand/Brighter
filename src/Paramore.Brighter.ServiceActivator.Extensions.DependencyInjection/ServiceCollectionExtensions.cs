using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging;

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
        public static IBrighterBuilder AddServiceActivator(
            this IServiceCollection services,
            Action<ServiceActivatorOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new ServiceActivatorOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
            services.AddSingleton<IBrighterOptions>(options);

            services.AddSingleton<IDispatcher>(BuildDispatcher);

            return ServiceCollectionExtensions.BrighterHandlerBuilder(services, options);
        }

        private static Dispatcher BuildDispatcher(IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            ApplicationLogging.LoggerFactory = loggerFactory;

            var options = serviceProvider.GetService<ServiceActivatorOptions>();

            Func<IAmACommandProcessorProvider> providerFactory;

            if (options.UseScoped)
            {
                providerFactory = () =>  new ScopedCommandProcessorProvider(serviceProvider);
            }
            else
            {
                var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
                providerFactory = () => new CommandProcessorProvider(commandProcessor);
            }

            var dispatcherBuilder = DispatchBuilder.With().CommandProcessorFactory(providerFactory);

            var messageMapperRegistry = ServiceCollectionExtensions.MessageMapperRegistry(serviceProvider);
            
            return dispatcherBuilder.MessageMappers(messageMapperRegistry)
                .DefaultChannelFactory(options.ChannelFactory)
                .Subscriptions(options.Subscriptions).Build();
        }
    }
   
}
