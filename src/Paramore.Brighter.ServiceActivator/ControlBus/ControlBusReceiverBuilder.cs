#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Transactions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Ports;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;

//Needs a different namespace to the DispatchBuilder to avoid collisions
namespace Paramore.Brighter.ServiceActivator.ControlBus
{
    /// <summary>
    /// Class ControlBusBuilder.
    /// </summary>
    public class ControlBusReceiverBuilder : INeedADispatcher, INeedAProducerRegistryFactory, INeedAChannelFactory, IAmADispatchBuilder
    {
        /// <summary>
        /// The configuration
        /// </summary>
        public const string CONFIGURATION = "configuration";
        /// <summary>
        /// The heartbeat
        /// </summary>
        public const string HEARTBEAT = "heartbeat";

        private IAmAPublicationFinder _publicationFinder = new FindPublicationByPublicationTopicOrRequestType();
        private IAmAChannelFactory? _channelFactory;
        private IDispatcher? _dispatcher;
        private IAmAProducerRegistryFactory? _producerRegistryFactory;

        /// <summary>
        /// We need a dispatcher to pull messages off the control bus and dispatch them out to control bus handlers.
        /// This may use the same broker we use for application messages, but we might choose to use a different
        /// broker so that we do not create additional load on the application message broker, or because we are concerned
        /// that application load could hinder monitoring
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>paramore.brighter.serviceactivator.controlbus.INeedAChannelFactory .</returns>
        public INeedAProducerRegistryFactory Dispatcher(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            return this;
        }

        public INeedAProducerRegistryFactory PublishFinder(IAmAPublicationFinder publicationFinder)
        {
            _publicationFinder = publicationFinder;
            return this;
        }

        /// <summary>
        /// The Control Bus may use a Request-Reply pattern over a Publish-Subscribe pattern, frr example a Heartbeat ot Trace
        /// Message. To enable us to reply we need to have an <see cref="IAmAMessageProducer"/> instance that lets us respond to
        /// the sender (over the control bus).
        /// </summary>
        /// <param name="producerRegistryFactory"></param>
        /// <returns></returns>
        public INeedAChannelFactory ProducerRegistryFactory(IAmAProducerRegistryFactory producerRegistryFactory)
        {
            _producerRegistryFactory = producerRegistryFactory;
            return this;
        }

        /// <summary>
        /// The channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="channelFactory">The channel factory.</param>
        /// <returns>INeedAListOfSubcriptions.</returns>
        public IAmADispatchBuilder ChannelFactory(IAmAChannelFactory channelFactory)
        {
            _channelFactory = channelFactory;
            return this;
        }


        /// <summary>
        /// Begins the progressive interface.
        /// </summary>
        /// <returns>INeedALogger.</returns>
        public static INeedADispatcher With()
        {
            return new ControlBusReceiverBuilder();
        }

        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <returns>Dispatcher.</returns>
        public Dispatcher Build(string hostName)
        {
            // We want to register policies, messages and handlers for receiving built in commands. It's simple enough to do this for
            // the registries, but we cannot know your HandlerFactory implementation in order to insert. So we have to rely on
            // an internal HandlerFactory to build these for you.
            // We also need to  pass the supervised dispatcher as a dependency to our command handlers, so this allows us to manage
            // the injection of the dependency as part of our handler factory
            
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry
            {
#pragma warning disable CS0618 // Type or member is obsolete
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.RETRYPOLICY, retryPolicy }
#pragma warning restore CS0618 // Type or member is obsolete
            };

            var resiliencePipeline = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();
            
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<ConfigurationCommand, ConfigurationCommandHandler>();
            subscriberRegistry.Register<HeartbeatRequest, HeartbeatRequestCommandHandler>();
            
            var incomingMessageMapperRegistry = new MessageMapperRegistry(
            new ControlBusMessageMapperFactory(), null
                );
            incomingMessageMapperRegistry.Register<ConfigurationCommand, ConfigurationCommandMessageMapper>();
            incomingMessageMapperRegistry.Register<HeartbeatRequest, HeartbeatRequestCommandMessageMapper>();

            var outgoingMessageMapperRegistry = new MessageMapperRegistry(
                new ControlBusMessageMapperFactory(), null
                );
            outgoingMessageMapperRegistry.Register<HeartbeatReply, HeartbeatReplyCommandMessageMapper>();

            if (_producerRegistryFactory is null)
                throw new ArgumentException("Producer Registry Factory must not be null.");
            
            var producerRegistry = _producerRegistryFactory.Create();

            var outbox = new SinkOutboxSync();
            
            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry: producerRegistry,
                resiliencePipelineRegistry: new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                mapperRegistry: outgoingMessageMapperRegistry,
                messageTransformerFactory: new EmptyMessageTransformerFactory(),
                messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(), 
                tracer: new BrighterTracer(),   //TODO: Do we need to pass in a tracer?
                outbox: outbox,
                outboxCircuitBreaker: new InMemoryOutboxCircuitBreaker(),
                publicationFinder: _publicationFinder
            );

            if (_dispatcher is null) throw new ArgumentException("Dispatcher must not be null");

            CommandProcessor? commandProcessor = null;
            
            commandProcessor = CommandProcessorBuilder.StartNew()
                .Handlers(new HandlerConfiguration(subscriberRegistry, new ControlBusHandlerFactorySync(_dispatcher, () => commandProcessor)))
                .Resilience(resiliencePipeline, policyRegistry)
                .ExternalBus(ExternalBusType.FireAndForget, mediator)
                .ConfigureInstrumentation(null, InstrumentationOptions.None)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(new InMemorySchedulerFactory())
                .Build();
            
            // These are the control bus channels, we hardcode them because we want to know they exist, but we use
            // a base naming scheme to allow centralized management.
            var subscriptions = new Subscription[]
            {
                new Subscription<ConfigurationCommand>(
                    subscriptionName: new SubscriptionName($"{hostName}.{CONFIGURATION}"),
                    channelName: new ChannelName($"{hostName}.{CONFIGURATION}"),
                    routingKey: new RoutingKey($"{hostName}.{CONFIGURATION}")),
                new Subscription<HeartbeatRequest>(
                    subscriptionName: new SubscriptionName($"{hostName}.{HEARTBEAT}"),
                    channelName: new ChannelName($"{hostName}.{HEARTBEAT}"),
                    routingKey: new RoutingKey($"{hostName}.{HEARTBEAT}"))
            };

            if (_channelFactory is null) throw new ArgumentException("Channel Factory must not be null");
            
            return DispatchBuilder.StartNew()
                .CommandProcessor(commandProcessor, new InMemoryRequestContextFactory()
                )
                .MessageMappers(incomingMessageMapperRegistry, null, null, null)
                .ChannelFactory(_channelFactory)                                        
                .Subscriptions(subscriptions)
                .NoInstrumentation()
                .Build();
        }


        /// <summary>
        /// We do not track outgoing control bus messages - so this acts as a sink for such messages
        /// </summary>
        private sealed class SinkOutboxSync : IAmAnOutboxSync<Message, CommittableTransaction>
        {
            public IAmABrighterTracer? Tracer { private get; set; } 
            
            public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
            {
                //discard message
            }

            public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1, IAmABoxTransactionProvider<CommittableTransaction>? transactionProvider = null)
            {
               //discard message 
            }
            
            public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
            {
                //ignore
            }

            public Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
            {
                 return new Message(){Header = new MessageHeader("",new RoutingKey(""), MessageType.MT_NONE)};
            }

            public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null)
            {
                //ignore
            }

            public IEnumerable<Message> DispatchedMessages(
                TimeSpan millisecondsDispatchedSince, 
                RequestContext requestContext,
                int pageSize = 100, 
                int pageNumber = 1,
                int outboxTimeout = -1, 
                Dictionary<string, object>? args = null
            )
            {
                return [];
            }

            public IEnumerable<Message> OutstandingMessages(
                TimeSpan dispatchedSince, 
                RequestContext? requestContext,
                int pageSize = 100, 
                int pageNumber = 1,
                IEnumerable<RoutingKey>? trippedTopics = null,
                Dictionary<string, object>? args = null)
            {
                return []; 
            }


            public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince)
            {
               return []; 
            }
        }
    }

    /// <summary>
    /// Interface INeedADispatcher
    /// </summary>
    public interface INeedADispatcher
    {
        /// <summary>
        /// This is the main dispatcher for the service. A control bus supervises this dispatcher (even though it is a dispatcher itself).
        /// We provide this dependency to the control bus so that we can manage it.
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>INeedAChannelFactory.</returns>
        INeedAProducerRegistryFactory Dispatcher(IDispatcher dispatcher);
    }

    public interface INeedAProducerRegistryFactory
    {
        INeedAProducerRegistryFactory PublishFinder(IAmAPublicationFinder publicationFinder);
        INeedAChannelFactory ProducerRegistryFactory(IAmAProducerRegistryFactory producerRegistryFactory);
    }

    /// <summary>
    /// Interface INeedAChannelFactory
    /// </summary>
    public interface INeedAChannelFactory
    {
        /// <summary>
        /// The channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="channelFactory">The channel factory.</param>
        /// <returns>INeedAListOfSubcriptions.</returns>
        IAmADispatchBuilder ChannelFactory(IAmAChannelFactory channelFactory);
    }

    /// <summary>
    /// Interface IAmADispatchBuilder
    /// </summary>
    public interface IAmADispatchBuilder
    {
        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <returns>Dispatcher.</returns>
        Dispatcher Build(string hostName);
    }
}
