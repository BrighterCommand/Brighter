using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public static class ServiceActivatorServiceCollectionExtensions
    {
        public static IServiceActivatorBuilder AddServiceActivator(
            this IServiceCollection services,
            Action<ServiceActivatorOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new ServiceActivatorOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services, options.HandlerLifetime);
            services.AddSingleton<ServiceCollectionSubscriberRegistry>(subscriberRegistry);

            services.AddSingleton<IAmACommandProcessor>(BuildCommandProcessor);

            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services, options.MapperLifetime);
            services.AddSingleton<ServiceCollectionMessageMapperRegistry>(mapperRegistry);
            

            services.AddSingleton<IDispatcher>(BuildDispatcher);

            return new ServiceCollectionServiceActivatorBuilder(services, subscriberRegistry, mapperRegistry);
        }

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<ServiceActivatorOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

            var policyBuilder = CommandProcessorBuilder.With()
                .Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            var builder = options.MessagingConfiguration == null
                ? messagingBuilder.NoTaskQueues()
                : messagingBuilder.TaskQueues(options.MessagingConfiguration);

            var commandProcessor = builder
                .RequestContextFactory(options.RequestContextFactory)
                .Build();

            return commandProcessor;
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