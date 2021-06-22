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

            return BrighterHandlerBuilder(services, options, 
                provider => (new ServiceProviderHandlerFactory(provider), new ServiceProviderHandlerFactory(provider)));
        }

        public static IBrighterHandlerBuilder BrighterHandlerBuilder(IServiceCollection services, BrighterOptions options, 
            Func<IServiceProvider, (IAmAHandlerFactory, IAmAHandlerFactoryAsync)> serviceProviderHandlerFactory)
        {
            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
            services.AddSingleton(subscriberRegistry);

            services.Add(new ServiceDescriptor(typeof(IAmACommandProcessor),
                provider =>
                {
                    (IAmAHandlerFactory handlerFactory, IAmAHandlerFactoryAsync handlerFactoryAsync) = serviceProviderHandlerFactory(provider);
                    return BuildCommandProcessor(provider, handlerFactory, handlerFactoryAsync);
                }, options.CommandProcessorLifetime));
            
            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services);
            services.AddSingleton<ServiceCollectionMessageMapperRegistry>(mapperRegistry);

            return new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        }

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider, IAmAHandlerFactory handlerFactory, IAmAHandlerFactoryAsync handlerFactoryAsync)
        {
            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactoryAsync);

            var messageMapperRegistry = MessageMapperRegistry(provider);

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
                    : messagingBuilder.TaskQueues(new MessagingConfiguration(options.BrighterMessaging.OutBox,
                        options.BrighterMessaging.AsyncOutBox, options.BrighterMessaging.Producer,
                        options.BrighterMessaging.AsyncProducer, messageMapperRegistry));
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
                            options.BrighterMessaging.OutBox,
                            options.BrighterMessaging.Producer, messageMapperRegistry,
                            responseChannelFactory: options.ChannelFactory))
                        : messagingBuilder.TaskQueues(new MessagingConfiguration(options.BrighterMessaging.OutBox,
                            options.BrighterMessaging.AsyncOutBox, options.BrighterMessaging.Producer,
                            options.BrighterMessaging.AsyncProducer, messageMapperRegistry));
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
        public IAmAnOutbox<Message> OutBox { get; }
        public IAmAnOutboxAsync<Message> AsyncOutBox { get; }
        public IAmAMessageProducer Producer { get; }
        public IAmAMessageProducerAsync AsyncProducer { get; }

        /// <summary>
        /// Use Request Reply Queues if Channel Factory is also set
        /// </summary>
        public bool UseRequestReplyQueues { get; }

        /// <summary>
        /// Constructor for use with a Producer
        /// </summary>
        /// <param name="outBox">The outbox to store messages - use InMemoryInbox if you do not require a persistent outbox</param>
        /// <param name="asyncOutBox">The outbox to store messages - use InMemoryInbox if you do not require a persistent outbox</param>
        /// <param name="producer">The Message producer</param>
        /// <param name="asyncProducer">The Message producer's async interface</param>
        /// <param name="useRequestReplyQueues">Use Request Reply Queues - This will need to set a Channel Factory as well.</param>
        public BrighterMessaging(IAmAnOutbox<Message> outBox, IAmAnOutboxAsync<Message> asyncOutBox, IAmAMessageProducer producer, IAmAMessageProducerAsync asyncProducer, bool useRequestReplyQueues = true)
        {
            OutBox = outBox;
            AsyncOutBox = asyncOutBox;
            Producer = producer;
            AsyncProducer = asyncProducer;
            UseRequestReplyQueues = useRequestReplyQueues;
        }

        /// <summary>
        /// Simplified constructor - we
        /// </summary>
        /// <param name="outbox">The outbox</param>
        /// <param name="producer">Producer</param>
        /// <param name="useRequestReplyQueues">Use Request Reply Queues - This will need to set a Channel Factory as well.</param>
        public BrighterMessaging(IAmAnOutbox<Message> outbox, IAmAMessageProducer producer, bool useRequestReplyQueues = true)
        {
            OutBox = outbox;
            if (outbox is IAmAnOutboxAsync<Message> outboxAsync) AsyncOutBox = outboxAsync;
            Producer = producer;
            if (producer is IAmAMessageProducerAsync producerAsync) AsyncProducer = producerAsync;
            UseRequestReplyQueues = useRequestReplyQueues;
        }
    }
}
