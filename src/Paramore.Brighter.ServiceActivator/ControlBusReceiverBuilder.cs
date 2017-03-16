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
using Paramore.Brighter.ServiceActivator.Ports;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;
using Polly;

//Needs a different namespace to the DispatchBuilder to avoid collisions
namespace Paramore.Brighter.ServiceActivator.ControlBus
{
    /// <summary>
    /// Class ControlBusBuilder.
    /// </summary>
    public class ControlBusReceiverBuilder : INeedADispatcher, INeedAMessageProducerFactory, INeedAChannelFactory, IAmADispatchBuilder
    {
        /// <summary>
        /// The configuration
        /// </summary>
        public const string CONFIGURATION = "configuration";
        /// <summary>
        /// The heartbeat
        /// </summary>
        public const string HEARTBEAT = "heartbeat";

        private IAmAChannelFactory _channelFactory;
        private IDispatcher _dispatcher;
        private IAmAMessageProducerFactory _producerFactory;

        /// <summary>
        /// We need a dispatcher to pull messages off the control bus and dispatch them out to control bus handlers.
        /// This may use the same broker we use for application messages, but we might choose to use a different
        /// broker so that we do not create additional load on the application message broker, or because we are concerned
        /// that application load could hinder monitoring
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <returns>paramore.brighter.serviceactivator.controlbus.INeedAChannelFactory .</returns>
        public INeedAMessageProducerFactory Dispatcher(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            return this;
        }

        /// <summary>
        /// The Control Bus may use a Request-Reply pattern over a Publish-Subscribe pattern, frr example a Heartbear ot Trace
        /// Message. To enable us to reply we need to have an <see cref="IAmAMessageProducer"/> instance that lets us respond to
        /// the sender (over the control bus).
        /// </summary>
        /// <param name="producerFactory"></param>
        /// <returns></returns>
        public INeedAChannelFactory ProducerFactory(IAmAMessageProducerFactory producerFactory)
        {
            _producerFactory = producerFactory;
            return this;
        }

        /// <summary>
        /// The channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="channelFactory">The channel factory.</param>
        /// <returns>INeedAListOfConnections.</returns>
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
            var connectionsConfiguration = new List<Connection>();

            /* 
             * These are the control bus channels, we hardcode them because we want to know they exist, but we use
            a base naming scheme to allow centralized management.
             */

            var connectionConfiguration = new Connection(
                new ConnectionName(hostName + "." + CONFIGURATION),
                _channelFactory,
                typeof(ConfigurationCommand),
                new ChannelName(hostName + "." + CONFIGURATION),
                hostName + "." + CONFIGURATION
                );
            //var connectionConfiguration = new ConnectionConfiguration()
            //{ 
            //    ChannelName = hostName  + "." + CONFIGURATION,
            //    ConnectionName = hostName  + "." + CONFIGURATION,
            //    IsDurable = true,
            //    DataType = typeof(ConfigurationCommand).FullName,
            //    RoutingKey = hostName + "." + CONFIGURATION,
            //};
            connectionsConfiguration.Add(connectionConfiguration);

            var heartbeatElement = new Connection(
                new ConnectionName(hostName + "." + HEARTBEAT),
                _channelFactory,
                typeof(HeartbeatRequest),
                new ChannelName(hostName + "." + HEARTBEAT),
                hostName + "." + HEARTBEAT,
                isDurable:false
                );

            //var heartbeatElement = new ConnectionConfiguration
            //{
            //    ChannelName = hostName  + "." + HEARTBEAT,
            //    ConnectionName = hostName  + "." + HEARTBEAT,
            //    IsDurable = false,
            //    DataType = typeof(HeartbeatRequest).FullName,
            //    RoutingKey = hostName + "." + HEARTBEAT,
            //};
            connectionsConfiguration.Add(heartbeatElement);

            /* We want to register policies, messages and handlers for receiving built in commands. It's simple enough to do this for
             the registries, but we cannot know your HandlerFactory implementation in order to insert. So we have to rely on
             an internal HandlerFactory to build these for you.
             
             * We also need to  pass the supervised dispatcher as a dependency to our command handlers, so this allows us to manage
             * the injection of the dependency as part of our handler factory
             
             */
            
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

            var policyRegistry = new PolicyRegistry()
            {
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy},
                {CommandProcessor.RETRYPOLICY, retryPolicy}
            };


            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<ConfigurationCommand, ConfigurationCommandHandler>();
            subscriberRegistry.Register<HeartbeatRequest, HeartbeatRequestCommandHandler>();
            
            var incomingMessageMapperRegistry = new MessageMapperRegistry(new ControlBusMessageMapperFactory());
            incomingMessageMapperRegistry.Register<ConfigurationCommand, ConfigurationCommandMessageMapper>();
            incomingMessageMapperRegistry.Register<HeartbeatRequest, HeartbeatRequestCommandMessageMapper>();

            var outgoingMessageMapperRegistry = new MessageMapperRegistry(new ControlBusMessageMapperFactory());
            outgoingMessageMapperRegistry.Register<HeartbeatReply, HeartbeatReplyCommandMessageMapper>();

            //TODO: It doesn't feel quite right that we have to pass this in for both dispatcher channel factory and task queue configuration
            //as we should be over same broker. But, so far, refactoring either ends up exposing properties of the channel factory, which we don't want
            //or stalling on the need for channel factory to be broker defined. It is possible the fix is to drop channel factory in favour of passing
            //in producer and sender. But that's a breaking change to the builder, so contemplating for now. 

            var producer = _producerFactory.Create();

            var messageStore = new SinkMessageStore();

            CommandProcessor commandProcessor = null;
            commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, new ControlBusHandlerFactory(_dispatcher, () => commandProcessor)))
                .Policies(policyRegistry: policyRegistry)
                .TaskQueues(new MessagingConfiguration(messageStore, producer, outgoingMessageMapperRegistry))
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            return DispatchBuilder
                .With()
                .CommandProcessor(commandProcessor)
                .MessageMappers(incomingMessageMapperRegistry)
                .ChannelFactory(_channelFactory)
                .Connections(connectionsConfiguration)
                .Build();
        }


        /// <summary>
        /// We do not track outgoing control bus messages - so this acts as a sink for such messages
        /// </summary>
        private class SinkMessageStore : IAmAMessageStore<Message>
        {
            public void Add(Message message, int messageStoreTimeout = -1)
            {
                //discard message
            }

            public Message Get(Guid messageId, int messageStoreTimeout = -1)
            {
                 return null;
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
        INeedAMessageProducerFactory Dispatcher(IDispatcher dispatcher);
    }

    public interface INeedAMessageProducerFactory
    {
        INeedAChannelFactory ProducerFactory(IAmAMessageProducerFactory producerFactory);
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
        /// <returns>INeedAListOfConnections.</returns>
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
