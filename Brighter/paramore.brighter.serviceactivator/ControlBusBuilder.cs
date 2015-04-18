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

using System.Collections.Generic;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.ServiceActivatorConfiguration;

namespace paramore.brighter.serviceactivator.controlbus
{
    /// <summary>
    /// Class ControlBusBuilder.
    /// </summary>
    public class ControlBusBuilder : INeedALogger, INeedACommandProcessor, INeedAChannelFactory, IAmADispatchBuilder
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
        private CommandProcessor _commandProcessor;
        private IAmAChannelFactory _channelFactory;

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
        /// The command processor used to send and publish messages to handlers by the service activator.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <returns>INeedAMessageMapper.</returns>
        public INeedAChannelFactory CommandProcessor(CommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
            return this;
        }

        /// <summary>
        /// The logger to use to report from the Dispatcher.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>INeedACommandProcessor.</returns>
        public INeedACommandProcessor Logger(ILog logger)
        {
            _logger = logger;
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
                DataType = typeof(ConfigurationCommand).AssemblyQualifiedName,
                RoutingKey = hostName + "." + CONFIGURATION,
            };
            connections.Add(configurationElement);

            var heartbeatElement = new ConnectionElement
            {
                ChannelName = HEARTBEAT,
                ConnectionName = HEARTBEAT,
                DataType = typeof(HeartBeatCommand).AssemblyQualifiedName,
                RoutingKey = hostName + "." + HEARTBEAT,
            };
            connections.Add(heartbeatElement);

            return DispatchBuilder
                .With()
                .Logger(_logger)
                .CommandProcessor(_commandProcessor)
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
            return new ControlBusBuilder();
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
        INeedACommandProcessor Logger(ILog logger);
    }

    /// <summary>
    /// Interface INeedACommandProcessor
    /// </summary>
    public interface INeedACommandProcessor
    {
        /// <summary>
        /// The command processor used to send and publish messages to handlers by the service activator.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <returns>INeedAMessageMapper.</returns>
        INeedAChannelFactory CommandProcessor(CommandProcessor commandProcessor);
    }

    /// <summary>
    /// Interface INeedAChannelFactory
    /// </summary>
    public interface INeedAChannelFactory
    {
        /// <summary>
        /// {D255958A-8513-4226-94B9-080D98F904A1}The channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP
        /// {D255958A-8513-4226-94B9-080D98F904A1}needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// {D255958A-8513-4226-94B9-080D98F904A1}layer. We provide an implementation for RabbitMQ for example.
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
