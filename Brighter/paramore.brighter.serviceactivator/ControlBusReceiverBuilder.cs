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
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator.Ports;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Handlers;
using paramore.brighter.serviceactivator.ServiceActivatorConfiguration;
using Polly;

//Needs a different namespace to the DispatchBuilder to avoid collisions
namespace paramore.brighter.serviceactivator.controlbus
{
    /// <summary>
    /// Class ControlBusBuilder.
    /// </summary>
    public class ControlBusReceiverBuilder : INeedALogger, INeedADispatcher, INeedAChannelFactory, IAmADispatchBuilder
    {
        /// <summary>
        /// The configuration
        /// </summary>
        public const string CONFIGURATION = "configuration";
        /// <summary>
        /// The heartbeat
        /// </summary>
        public const string HEARTBEAT = "heartbeat";

        private ILog _logger;
        private IAmAChannelFactory _channelFactory;
        private IDispatcher _dispatcher;

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
        /// The logger to use to report from the Dispatcher.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>INeedACommandProcessor.</returns>
        public INeedADispatcher Logger(ILog logger)
        {
            _logger = logger;
            return this;
        }

        public INeedAChannelFactory Dispatcher(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            return this;
        }

        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <returns>Dispatcher.</returns>
        public Dispatcher Build(string hostName)
        {
            var connections = new List<ConnectionElement>();

            /* 
             * These are the control bus channels, we hardcode them because we want to know they exist, but we use
            a base naming scheme to allow centralized management.
             */

            var configurationElement = new ConnectionElement
            {
                ChannelName = CONFIGURATION,
                ConnectionName = CONFIGURATION,
                IsDurable = true,
                DataType = typeof(ConfigurationCommand).AssemblyQualifiedName,
                RoutingKey = hostName + "." + CONFIGURATION,
            };
            connections.Add(configurationElement);

            var heartbeatElement = new ConnectionElement
            {
                ChannelName = HEARTBEAT,
                ConnectionName = HEARTBEAT,
                IsDurable = false,
                DataType = typeof(HeartBeatCommand).AssemblyQualifiedName,
                RoutingKey = hostName + "." + HEARTBEAT,
            };
            connections.Add(heartbeatElement);

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
            

            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, new ControlBusHandlerFactory(_dispatcher, _logger)))
                .Policies(policyRegistry)
                .Logger(_logger)
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();


            return DispatchBuilder
                .With()
                .Logger(_logger)
                .CommandProcessor(commandProcessor)
                .MessageMappers(new MessageMapperRegistry(new ControlBusMessageMapperFactory()))
                .ChannelFactory(_channelFactory)
                .ConnectionsFromElements(connections)
                .Build();
        }

        /// <summary>
        /// Withes this instance.
        /// </summary>
        /// <returns>INeedALogger.</returns>
        public static INeedALogger With()
        {
            return new ControlBusReceiverBuilder();
        }
    }

    /// <summary>
    /// Interface INeedALogger
    /// </summary>
    public interface INeedALogger
    {
        /// <summary>
        /// The logger to use to report from the Dispatcher.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>INeedACommandProcessor.</returns>
        INeedADispatcher Logger(ILog logger);
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
        INeedAChannelFactory Dispatcher(IDispatcher dispatcher);
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
