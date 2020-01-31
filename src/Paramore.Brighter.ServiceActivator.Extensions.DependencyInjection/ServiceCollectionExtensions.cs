using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public static class  ServiceActivatorServiceCollectionExtensions 
    {
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
                .Connections(options.Connections).Build();
        }
    }
   
}