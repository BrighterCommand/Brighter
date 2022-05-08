using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        
        /// <summary>
        /// Will add Brighter into the .NET IoC Contaner - ServiceCollection
        /// Registers singletons with the service collection :-
        ///  - BrighterOptions - how should we configure Brighter
        ///  - SubscriberRegistry - what handlers subscribe to what requests
        ///  - MapperRegistry - what mapppers translate what messages
        ///  - InMemoryOutbox - Optional - if an in memory outbox is selected
        /// </summary>
        /// <param name="services">The IoC container to update</param>
        /// <param name="configure">A callback that defines what options to set when Brighter is built</param>
        /// <returns>A builder that can be used to populate the IoC container with handlers and mappers by inspection - used by built in factory from CommandProcessor</returns>
        /// <exception cref="ArgumentNullException">Thrown if we have no IoC provided ServiceCollection</exception>
        public static IBrighterBuilder AddBrighter(this IServiceCollection services, Action<BrighterOptions> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var options = new BrighterOptions();
            configure?.Invoke(options);
            services.AddSingleton<IBrighterOptions>(options);

            return BrighterHandlerBuilder(services, options);
        }
        
        /// <summary>
        /// Normally you want to call AddBrighter from client code, and not this method. Public only to support Service Activator extensions
        /// Registers singletons with the service collection :-
        ///  - SubscriberRegistry - what handlers subscribe to what requests
        ///  - MapperRegistry - what mapppers translate what messages
        /// </summary>
        /// <param name="services">The IoC container to update</param>
        /// <param name="configure">A callback that defines what options to set when Brighter is built</param>
        /// <returns>A builder that can be used to populate the IoC container with handlers and mappers by inspection - used by built in factory from CommandProcessor</returns>
        public static IBrighterBuilder BrighterHandlerBuilder(IServiceCollection services, BrighterOptions options)
        {
            var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services, options.HandlerLifetime);
            services.AddSingleton<ServiceCollectionSubscriberRegistry>(subscriberRegistry);

            services.Add(new ServiceDescriptor(typeof(IAmACommandProcessor), BuildCommandProcessor, options.CommandProcessorLifetime));

            var mapperRegistry = new ServiceCollectionMessageMapperRegistry(services, options.MapperLifetime);
            services.AddSingleton<ServiceCollectionMessageMapperRegistry>(mapperRegistry);

            return new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        }
         
         /// <summary>
        /// Use an external Brighter Outbox to store messages Posted to another process (evicts based on age and size).
        /// Advantages: By using the same Db to store both any state changes for your app, and outgoing messages you can create a transaction that spans both
        ///  your state change and writing to an outbox [use DepositPost to store]. Then a sweeper process can look for message not flagged as sent and send them.
        ///  For low latency just send after the transaction with ClearOutbox, for higher latency just let the sweeper run in the background.
        ///  The outstanding messages dispatched this way can be sent from any producer that runs a sweeper process and so it not tied to the lifetime of the
        ///  producer, offering guaranteed, at least once, delivery.
        ///  NOTE: there may be a database specific Use*OutBox available. If so, use that in preference to this generic method
        /// If not null, registers singletons with the service collection :-
        ///  - IAmAnOutboxSync - what messages have we posted
        ///  - ImAnOutboxAsync - what messages have we posted (async pipeline compatible)
        /// </summary>
        /// <param name="brighterBuilder">The Brighter builder to add this option to</param>
        /// <param name="outbox">The outbox provider - if your outbox supports both sync and async options, just provide this and we will register both</param>
        /// <returns></returns>
        public static IBrighterBuilder UseExternalOutbox(this IBrighterBuilder brighterBuilder, IAmAnOutbox<Message> outbox = null)
        {
            if (outbox is IAmAnOutboxSync<Message>)
            {
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), _ => outbox, ServiceLifetime.Singleton));
            }

            if (outbox is IAmAnOutboxAsync<Message>)
            {
                brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), _ => outbox, ServiceLifetime.Singleton));
            }

            return brighterBuilder;
             
        }

         /// <summary>
         /// Uses an external Brighter Inbox to record messages received to allow "once only" or diagnostics (how did we get here?)
         /// Advantages: by using an external inbox then you can share "once only" across multiple threads/processes and support a competing consumer
         /// model; an internal inbox is useful for testing but outside of single consumer scenarios won't work as intended
         /// If not null, registers singletons with the service collecion :-
         ///  - IAmAnInboxSync - what messages have we received
         ///  - IAmAnInboxAsync - what messages have we received (async pipeline compatible)
         /// </summary>
         /// <param name="brighterBuilder">Extension method to support a fluent interface</param>
         /// <param name="inbox">The external inbox to use</param>
         /// <param name="inboxConfiguration">If this is null, configure by hand, if not, will auto-add inbox to handlers</param>
         /// <returns></returns>
         public static IBrighterBuilder UseExternalInbox(
             this IBrighterBuilder brighterBuilder, 
             IAmAnInbox inbox, InboxConfiguration inboxConfiguration = null, 
             ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
         {
             if (inbox is IAmAnInboxSync)
             {
                 brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnInboxSync), _ => inbox, serviceLifetime));
             }

             if (inbox is IAmAnInboxAsync)
             {
                 brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnInboxAsync), _ => inbox, serviceLifetime));
             }

             if (inboxConfiguration != null)
             {
                 brighterBuilder.Services.AddSingleton<InboxConfiguration>(inboxConfiguration);
             }

             return brighterBuilder;
         }

        /// <summary>
        /// Use the Brighter In-Memory Outbox to store messages Posted to another process (evicts based on age and size).
        /// Advantages: fast and no additional infrastructure required
        /// Disadvantages: The Outbox will not survive restarts, so messages not published by shutdown will not be flagged as not posted
        /// Registers singletons with the service collection :-
        ///  - InMemoryOutboxSync - what messages have we posted
        ///  - InMemoryOutboxAsync - what messages have we posted (async pipeline compatible)
        /// </summary>
        /// <param name="brighterBuilder">The builder we are adding this facility to</param>
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder UseInMemoryOutbox(this IBrighterBuilder brighterBuilder)
        {
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), _ => new InMemoryOutbox(), ServiceLifetime.Singleton));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), _ => new InMemoryOutbox(), ServiceLifetime.Singleton));

            return brighterBuilder;
        }

        /// <summary>
        /// Uses the Brighter In-Memory Inbox to store messages received to support once-only messaging and diagnostics
        /// Advantages: Fast and no additional infrastructure required
        /// Disadvantages: The inbox will not survive restarts, so messages will not be de-duped if received after a restart.
        ///                The inbox will not work across threads/processes so only works with a single performer/consumer.
        /// Registers singletons with the service collection:
        ///  - InMemoryInboxSync - what messages have we received
        ///  - InMemoryInboxAsync - what messages have we recived (async pipeline compatible)
        /// </summary>
        /// <param name="brighterBuilder"></param>
        /// <returns></returns>
        public static IBrighterBuilder UseInMemoryInbox(this IBrighterBuilder brighterBuilder)
        {
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnInboxSync), _ => new InMemoryInbox(), ServiceLifetime.Singleton));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnInboxAsync), _ => new InMemoryInbox(), ServiceLifetime.Singleton));

            return brighterBuilder;
        }

        /// <summary>
        /// An external bus is the use of Message Oriented Middleware (MoM) to dispatch a message between a producer and a consumer. The assumption is that this
        /// is being used for inter-process communication, for example the work queue pattern for distributing work, or between microservicves
        /// Registers singletons with the service collection :-
        ///  - Producer - the Gateway wrapping access to Middleware
        ///  - UseRpc - do we want to use Rpc i.e. a command blocks waiting for a response, over middleware
        /// </summary>
        /// <param name="brighterBuilder">The Brighter builder to add this option to</param>
        /// <param name="producerRegistry">The collection of producers - clients that connect to a specific transport</param>
        /// <param name="useRequestResponseQueues">Add support for RPC over MoM by using a reply queue</param>
        /// <param name="replyQueueSubscriptions">Reply queue subscription</param>
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder UseExternalBus(this IBrighterBuilder brighterBuilder, IAmAProducerRegistry producerRegistry, bool useRequestResponseQueues = false, IEnumerable<Subscription> replyQueueSubscriptions = null)
        {
            
            brighterBuilder.Services.AddSingleton<IAmAProducerRegistry>(producerRegistry);
            
            brighterBuilder.Services.AddSingleton<IUseRpc>(new UseRpc(useRequestResponseQueues, replyQueueSubscriptions));
            
            return brighterBuilder;
        }

        /// <summary>
        /// Configure a Feature Switch registry to control handlers to be feature switched at runtime
        /// </summary>
        /// <param name="brighterBuilder">The Brighter builder to add this option to</param>
        /// <param name="featureSwitchRegistry">The registry for handler Feature Switches</param>
        /// <returns>The Brighter builder to allow chaining of requests</returns>
        public static IBrighterBuilder UseFeatureSwitches(this IBrighterBuilder brighterBuilder, IAmAFeatureSwitchRegistry featureSwitchRegistry)
        {
            brighterBuilder.Services.AddSingleton(featureSwitchRegistry);
            return brighterBuilder;
        }
        
        /// <summary>
        /// Registers message mappers with the registry. Normally you don't need to call this, it is called by the builder for Brighter or the Service Activator
        /// Visibility is required for use from both
        /// </summary>
        /// <param name="provider">The IoC container to request the message mapper registry from</param>
        /// <returns>The message mapper registry, populated with any message mappers from the ioC container</returns>
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

        private static CommandProcessor BuildCommandProcessor(IServiceProvider provider)
        {
            var options = provider.GetService<IBrighterOptions>();
            var subscriberRegistry = provider.GetService<ServiceCollectionSubscriberRegistry>();
            var useRequestResponse = provider.GetService<IUseRpc>();

            var handlerFactory = new ServiceProviderHandlerFactory(provider);
            var handlerConfiguration = new HandlerConfiguration(subscriberRegistry, handlerFactory);

            var messageMapperRegistry = MessageMapperRegistry(provider);

            var outbox = provider.GetService<IAmAnOutboxSync<Message>>();
            var asyncOutbox = provider.GetService<IAmAnOutboxAsync<Message>>();
            var overridingConnectionProviderRegistry = provider.GetService<IAmABoxTransactionConnectionProviderRegistry>();

            if (outbox == null) outbox = new InMemoryOutbox();
            if (asyncOutbox == null) asyncOutbox = new InMemoryOutbox();

            var inboxConfiguration = provider.GetService<InboxConfiguration>();
            
            var producerRegistry = provider.GetService<IAmAProducerRegistry>();

            var needHandlers = CommandProcessorBuilder.With();

            var featureSwitchRegistry = provider.GetService<IAmAFeatureSwitchRegistry>();

            if (featureSwitchRegistry != null)
                needHandlers = needHandlers.ConfigureFeatureSwitches(featureSwitchRegistry);
            
            var policyBuilder = needHandlers.Handlers(handlerConfiguration);

            var messagingBuilder = options.PolicyRegistry == null
                ? policyBuilder.DefaultPolicy()
                : policyBuilder.Policies(options.PolicyRegistry);

            var loggerFactory = provider.GetService<ILoggerFactory>();
            ApplicationLogging.LoggerFactory = loggerFactory;

            var commandProcessor = AddExternalBusMaybe(
                    options, 
                    producerRegistry, 
                    messagingBuilder, 
                    messageMapperRegistry, 
                    inboxConfiguration, 
                    outbox, 
                    overridingConnectionProviderRegistry, 
                    useRequestResponse)
                .RequestContextFactory(options.RequestContextFactory)
                .Build();

            return commandProcessor;
        }

        private enum ExternalBusType
        {
            None = 0,
            FireAndForget = 1,
            RPC = 2
        }
        
        private static INeedARequestContext AddExternalBusMaybe(
            IBrighterOptions options, 
            IAmAProducerRegistry producerRegistry, 
            INeedMessaging messagingBuilder,
            MessageMapperRegistry messageMapperRegistry, 
            InboxConfiguration inboxConfiguration, 
            IAmAnOutboxSync<Message> outbox,
            IAmABoxTransactionConnectionProviderRegistry overridingConnectionProviderRegistry, 
            IUseRpc useRequestResponse)
        {
            ExternalBusType externalBusType = GetExternalBusType(producerRegistry, useRequestResponse);

            if (externalBusType == ExternalBusType.None)
                return messagingBuilder.NoExternalBus();
            else if (externalBusType == ExternalBusType.FireAndForget)
                return messagingBuilder.ExternalBus(
                    new ExternalBusConfiguration(producerRegistry, messageMapperRegistry, useInbox: inboxConfiguration),
                    outbox,
                    overridingConnectionProviderRegistry);
            else if (externalBusType == ExternalBusType.RPC)
            {
                return messagingBuilder.ExternalRPC(
                    new ExternalBusConfiguration(
                        producerRegistry,
                        messageMapperRegistry,
                        responseChannelFactory: options.ChannelFactory,
                        useInbox: inboxConfiguration),
                    outbox,
                    useRequestResponse.ReplyQueueSubscriptions);
            }

            throw new ArgumentOutOfRangeException("The external bus type requested was not understood");
        }

        private static ExternalBusType GetExternalBusType(IAmAProducerRegistry producerRegistry, IUseRpc useRequestResponse)
        {
            var externalBusType = producerRegistry == null ? ExternalBusType.None : ExternalBusType.FireAndForget;
            if (externalBusType == ExternalBusType.FireAndForget && useRequestResponse.RPC) externalBusType = ExternalBusType.RPC;
            return externalBusType;
        }
    }
}
