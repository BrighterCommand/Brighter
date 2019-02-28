using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public static class ServiceActivatorServiceCollectionExtensions 
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

            var brighterHandlerBuilder = ServiceCollectionExtensions.BrighterHandlerBuilder(services, options);

            services.AddSingleton<IDispatcher>(BuildDispatcher);

            return brighterHandlerBuilder;
        }

        private static Dispatcher BuildDispatcher(IServiceProvider serviceProvider)
        {
            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            var options = serviceProvider.GetService<ServiceActivatorOptions>();

            var dispatcherBuilder = DispatchBuilder.With().CommandProcessor(commandProcessor);

            var serviceCollectionMessageMapperRegistry = serviceProvider.GetService<ServiceCollectionMessageMapperRegistry>();
            
            var messageMapperRegistry = new MessageMapperRegistry(new ServiceProviderMapperFactory(serviceProvider));

            foreach (var messageMapper in serviceCollectionMessageMapperRegistry)
            {
                messageMapperRegistry.Add(messageMapper.Key, messageMapper.Value);
            }
            
            return dispatcherBuilder.MessageMappers(messageMapperRegistry)
                .DefaultChannelFactory(options.ChannelFactory)
                .Connections(options.Connections).Build();
        }
    }
}