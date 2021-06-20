using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

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
        
        

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

            var messageMapperRegistry = MessageMapperRegistry(provider);

            var outbox = provider.GetService<IAmAnOutbox<Message>>();
            var asyncOutbox = provider.GetService<IAmAnOutboxAsync<Message>>();

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
                //TODO: Need to add async outbox 
                
                taskQueuesBuilder = options.BrighterMessaging == null
                    ? messagingBuilder.NoTaskQueues()
                    : messagingBuilder.TaskQueues(new MessagingConfiguration(options.BrighterMessaging.Producer,
                        options.BrighterMessaging.AsyncProducer, messageMapperRegistry), outbox);
            }
            else
            {
                if (options.BrighterMessaging == null)
                {
                    taskQueuesBuilder = messagingBuilder.NoTaskQueues();
                }
                else
                {
                    taskQueuesBuilder = options.BrighterMessaging.UseRequestReplyQueues
                        ? messagingBuilder.RequestReplyQueues(new MessagingConfiguration(
                            options.BrighterMessaging.Producer, messageMapperRegistry,
                            responseChannelFactory: options.ChannelFactory))
                        : messagingBuilder.TaskQueues(new MessagingConfiguration(options.BrighterMessaging.Producer,
                            options.BrighterMessaging.AsyncProducer, messageMapperRegistry), outbox);
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

    public class BrighterMessaging
    {
        public IAmAMessageProducer Producer { get; }
        public IAmAMessageProducerAsync AsyncProducer { get; }

        /// <summary>
        /// Use Request Reply Queues if Channel Factory is also set
        /// </summary>
        public bool UseRequestReplyQueues { get; }

        /// <summary>
        /// Constructor for use with a Producer
        /// </summary>
        /// <param name="producer">The Message producer</param>
        /// <param name="asyncProducer">The Message producer's async interface</param>
        /// <param name="useRequestReplyQueues">Use Request Reply Queues - This will need to set a Channel Factory as well.</param>
        public BrighterMessaging(IAmAMessageProducer producer, IAmAMessageProducerAsync asyncProducer, bool useRequestReplyQueues = true)
        {
            Producer = producer;
            AsyncProducer = asyncProducer;
            UseRequestReplyQueues = useRequestReplyQueues;
        }

        /// <summary>
        /// Simplified constructor - we
        /// </summary>
        /// <param name="producer">Producer</param>
        /// <param name="useRequestReplyQueues">Use Request Reply Queues - This will need to set a Channel Factory as well.</param>
        public BrighterMessaging(IAmAMessageProducer producer, bool useRequestReplyQueues = true)
        {
            Producer = producer;
            if (producer is IAmAMessageProducerAsync producerAsync) AsyncProducer = producerAsync;
            UseRequestReplyQueues = useRequestReplyQueues;
        }
    }
}
