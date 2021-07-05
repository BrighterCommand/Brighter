using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        private static bool s_useRequestReplyQueues = false;
        
        public static IBrighterHandlerBuilder AddBrighter(this IServiceCollection services, Action<BrighterOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new BrighterOptions();
            configure?.Invoke(options);
            services.AddSingleton<IBrighterOptions>(options);

            return BrighterHandlerBuilder(services, options);
        }
        public static IBrighterHandlerBuilder BrighterHandlerBuilder(IServiceCollection services, BrighterOptions options)
        {
            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
            services.AddSingleton<ServiceCollectionSubscriberRegistry>(subscriberRegistry);

            services.Add(new ServiceDescriptor(typeof(IAmACommandProcessor), BuildCommandProcessor, options.CommandProcessorLifetime));

            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services);
            services.AddSingleton<ServiceCollectionMessageMapperRegistry>(mapperRegistry);

            return new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        }

        public static IBrighterHandlerBuilder UseInMemoryOutbox(
            this IBrighterHandlerBuilder brighterBuilder, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), _ => new InMemoryOutbox(), serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), _ => new InMemoryOutbox(), serviceLifetime));

            return brighterBuilder;
        }

        public static IBrighterHandlerBuilder UseScopedCommandProcessor(this IBrighterHandlerBuilder brighterBuilder)
        {
            brighterBuilder.Services.AddScoped<IAmAHandlerFactory, ServiceProviderHandlerFactory>();
            brighterBuilder.Services.AddScoped<IAmAHandlerFactoryAsync, ServiceProviderHandlerFactory>();
            brighterBuilder.Services.AddScoped<IAmAScopedCommandProcessor, ScopedCommandProcessor>();
            
            return brighterBuilder;
        }

        public static IBrighterHandlerBuilder UseExternalBus(this IBrighterHandlerBuilder brighterBuilder, IAmAMessageProducer producer, bool useRequestReplyQueues = false)
        {
            brighterBuilder.Services.AddSingleton<IAmAMessageProducer>(producer);
            if(producer is IAmAMessageProducerAsync @async) brighterBuilder.Services.AddSingleton<IAmAMessageProducerAsync>(@async);

            s_useRequestReplyQueues = useRequestReplyQueues;
            
            return brighterBuilder;
        }
        
        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

            var messageMapperRegistry = MessageMapperRegistry(provider);

            var outbox = provider.GetService<IAmAnOutbox<Message>>();
            var asyncOutbox = provider.GetService<IAmAnOutboxAsync<Message>>();

            if (outbox == null) outbox = new InMemoryOutbox();
            if (asyncOutbox == null) asyncOutbox = new InMemoryOutbox();
            
            var producer = provider.GetService<IAmAMessageProducer>();
            var asyncProducer = provider.GetService<IAmAMessageProducerAsync>();

            var policyBuilder = CommandProcessorBuilder.With()
                .Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            var loggerFactory = provider.GetService<ILoggerFactory>();
            ApplicationLogging.LoggerFactory = loggerFactory;
            
            INeedARequestContext taskQueuesBuilder;
            if (options.ChannelFactory is null)
            {
                taskQueuesBuilder = producer == null
                    ? messagingBuilder.NoTaskQueues()
                    : messagingBuilder.TaskQueues(new MessagingConfiguration(producer, asyncProducer, messageMapperRegistry), outbox);
            }
            else
            {
                // If Producer has been added to DI
                if (producer == null)
                {
                    taskQueuesBuilder = messagingBuilder.NoTaskQueues();
                }
                else
                {
                    taskQueuesBuilder = s_useRequestReplyQueues
                        ? messagingBuilder.RequestReplyQueues(new MessagingConfiguration(
                            producer, messageMapperRegistry,
                            responseChannelFactory: options.ChannelFactory))
                        : messagingBuilder.TaskQueues(new MessagingConfiguration(producer, asyncProducer, messageMapperRegistry), outbox);
                }
            }

            var commandProcessor = taskQueuesBuilder
                .RequestContextFactory(options.RequestContextFactory)
                .Build();

            return commandProcessor;
        }

        public static MessageMapperRegistry MessageMapperRegistry(IServiceProvider provider)
        {
            var serviceCollectionMessageMapperRegistry = provider.GetService<ServiceCollectionMessageMapperRegistry>();

            var messageMapperRegistry = new MessageMapperRegistry(new ServiceProviderMapperFactory(provider));

            foreach (var messageMapper in serviceCollectionMessageMapperRegistry)
            {
                messageMapperRegistry.Add(messageMapper.Key, messageMapper.Value);
            }

            return messageMapperRegistry;
        }
    }
}
