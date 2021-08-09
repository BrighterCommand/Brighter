using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

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
        public static IBrighterHandlerBuilder AddServiceActivator(
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
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var options = serviceProvider.GetService<ServiceActivatorOptions>();

            var dispatcherBuilder = DispatchBuilder.With().CommandProcessor(commandProcessor);

            var messageMapperRegistry = ServiceCollectionExtensions.MessageMapperRegistry(serviceProvider);
            
            return dispatcherBuilder.MessageMappers(messageMapperRegistry)
                .DefaultChannelFactory(options.ChannelFactory)
                .Connections(options.Subscriptions).Build();
        }
    }
   
}
