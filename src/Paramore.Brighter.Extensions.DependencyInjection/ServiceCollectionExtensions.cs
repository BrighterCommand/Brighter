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

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory, handlerFactory);

            var messageMapperRegistry = MessageMapperRegistry(provider);

            var policyBuilder = CommandProcessorBuilder.With()
                .Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            
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
                taskQueuesBuilder = options.BrighterMessaging == null
                    ? messagingBuilder.NoTaskQueues()
                    : messagingBuilder.RequestReplyQueues(new MessagingConfiguration(options.BrighterMessaging.OutBox,
                        options.BrighterMessaging.Producer, messageMapperRegistry, responseChannelFactory: options.ChannelFactory));
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

        public BrighterMessaging(IAmAnOutbox<Message> outBox, IAmAnOutboxAsync<Message> asyncOutBox, IAmAMessageProducer producer, IAmAMessageProducerAsync asyncProducer)
        {
            OutBox = outBox;
            AsyncOutBox = asyncOutBox;
            Producer = producer;
            AsyncProducer = asyncProducer;
        }
    }
}
