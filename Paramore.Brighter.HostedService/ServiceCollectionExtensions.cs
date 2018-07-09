using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.AspNetCore;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.HostedService
{
    public static class ServiceActivatorServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder AddBrighter(this IServiceCollection services, Action<BrighterOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new BrighterOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            var subscriberRegistry = new AspNetSubscriberRegistry(services, options.HandlerLifetime);
            services.AddSingleton<AspNetSubscriberRegistry>(subscriberRegistry);

            services.AddSingleton<IAmACommandProcessor>(BuildCommandProcessor);

            return new AspNetHandlerBuilder(services, subscriberRegistry);
        }

        public static IServiceActivatorBuilder AddServiceActivator(this IServiceCollection services, Action<ServiceActivatorOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new ServiceActivatorOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            var subscriberRegistry = new AspNetSubscriberRegistry(services, options.HandlerLifetime);
            services.AddSingleton<AspNetSubscriberRegistry>(subscriberRegistry);

            services.AddSingleton<IAmACommandProcessor>(BuildCommandProcessor);

            var mapperRegistry = new HostMessageMapperRegistry(services, options.MapperLifetime);
            services.AddSingleton<HostMessageMapperRegistry>(mapperRegistry);

            services.AddSingleton<IDispatcher>(BuildDispatcher);

            return new ServiceActivatorBuilder(services, subscriberRegistry, mapperRegistry);
        }

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<ServiceActivatorOptions>();
            var subscriberRegistry = provider.GetService<AspNetSubscriberRegistry>();

           var handlerFactory = new AspNetHandlerFactory(provider);
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

            var logger = serviceProvider.GetService<ILogger<ServiceActivatorHostedService>>(); 
           
            var dispatcherBuilder = DispatchBuilder.With().CommandProcessor(commandProcessor);

            var mappers = serviceProvider.GetService<HostMessageMapperRegistry>();
            
            var messageMapperRegistry = new MessageMapperRegistry(new MapperFactory(serviceProvider));

            foreach (var mapper in mappers.GetAll())
            {
                messageMapperRegistry.Add(mapper.Key, mapper.Value);
                logger.LogInformation($"Adding mappers: Message '{mapper.Key}' - Mapper '{mapper.Value}'");
            }
           
            return dispatcherBuilder.MessageMappers(messageMapperRegistry)
                .DefaultChannelFactory(options.ChannelFactory)
                .Connections(options.Connections).Build();
        }
    }

   public class ServiceActivatorOptions : BrighterOptions
    {
        public IEnumerable<Connection> Connections { get; set; } = new List<Connection>();
        public IAmAChannelFactory ChannelFactory { get; set; } = new InMemoryChannelFactory();
        public ServiceLifetime MapperLifetime { get; set; } = ServiceLifetime.Transient;
    }
}