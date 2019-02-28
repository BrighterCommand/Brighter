using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder AddBrighter(this IServiceCollection services, Action<BrighterOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new BrighterOptions();
            configure?.Invoke(options);
            services.AddSingleton<IBrighterOptions>(options);

            //var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
            //services.AddSingleton<ServiceCollectionSubscriberRegistry>(subscriberRegistry);

            //if (options.HandlerLifetime == ServiceLifetime.Scoped)
            //    services.AddScoped<IAmACommandProcessor>(BuildCommandProcessor);
            //else
            //    services.AddSingleton<IAmACommandProcessor>(BuildCommandProcessor);

            //return new ServiceCollectionBrighterBuilder(services, subscriberRegistry);

            return BrighterHandlerBuilder(services, options);
        }
        public static IBrighterHandlerBuilder BrighterHandlerBuilder(IServiceCollection services, BrighterOptions options)
        {
            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
            services.AddSingleton<ServiceCollectionSubscriberRegistry>(subscriberRegistry);

            if (options.HandlerLifetime == ServiceLifetime.Scoped)
                services.AddScoped<IAmACommandProcessor>(BuildCommandProcessor);
            else
                services.AddSingleton<IAmACommandProcessor>(BuildCommandProcessor);

           // var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services, options.MapperLifetime);
            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services);
            services.AddSingleton<ServiceCollectionMessageMapperRegistry>(mapperRegistry);

            return new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        }

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

            var policyBuilder = CommandProcessorBuilder.With()
                .Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            var builder = options.BrighterMessaging == null
                ? messagingBuilder.NoTaskQueues()
                : messagingBuilder.TaskQueues(new MessagingConfiguration(options.BrighterMessaging.MessageStore, options.BrighterMessaging.Producer, ));

            var commandProcessor = builder
                .RequestContextFactory(options.RequestContextFactory)
                .Build();

            return commandProcessor;
        }

        //private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        //{
        //    var options = provider.GetService<BrighterOptions>();
        //    var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

        //    var handlerFactory = new ServiceProviderHandlerFactory(provider);
        //    var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

        //    var messageMapperRegistry = 

        //    var policyBuilder = CommandProcessorBuilder.With()
        //        .Handlers(handlerConfiguration);

        //    var messagingBuilder = options.PolicyRegistry == null
        //        ? policyBuilder.DefaultPolicy()
        //        : policyBuilder.Policies(options.PolicyRegistry);

        //    var builder = options.MessagingConfiguration == null
        //        ? messagingBuilder.NoTaskQueues()
        //        : messagingBuilder.TaskQueues(new MessagingConfiguration(messageStore, producer, messageMapperRegistry));

        //    var commandProcessor = builder
        //        .RequestContextFactory(options.RequestContextFactory)
        //        .Build();

        //    return commandProcessor;
        //}
    }

    public class BrighterMessaging
    {
        public IAmAMessageStore<Message> MessageStore { get; }
        public IAmAMessageProducer Producer { get; }

        public BrighterMessaging(IAmAMessageStore<Message> messageStore, IAmAMessageProducer producer)
        {
            MessageStore = messageStore;
            Producer = producer;
        }
    }
}