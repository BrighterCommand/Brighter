// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
// <copyright file="DispatchBuilder.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.ServiceActivatorConfiguraton;

namespace paramore.brighter.serviceactivator
{
    //TODO: The repeated use of the gateway and logger between this and the command processor is fugly
    //one option could be to use types not instances here, and do more assembly in build
    //currently configuration is a weak spot and has an early NH configuration mess feel to it

    /// <summary>
    /// Class DispatchBuilder.
    /// A fluent builder used to simplify construction of instances of the Dispatcher. Begin by calling With() and the syntax will then provide you with 
    /// progressive interfaces to manage the requirements for a complete Dispatcher via Intellisense in the IDE. The intent is to make it easier to
    /// recognize those dependencies that you need to configure
    /// </summary>
    public class DispatchBuilder : INeedALogger, INeedACommandProcessor, INeedAChannelFactory, INeedAMessageMapper, INeedAListOfConnections, IAmADispatchBuilder
    {
        private IAmAChannelFactory channelFactory;
        private IEnumerable<Connection> connections;
        private ILog logger;
        private CommandProcessor commandProcessor;
        private IAmAMessageMapperRegistry messageMapperRegistry;
        private DispatchBuilder() {}


        /// <summary>
        /// Begins the fluent interface 
        /// </summary>
        /// <returns>INeedALogger.</returns>
        public static INeedALogger With()
        {
            return new DispatchBuilder();
        }


        /// <summary>
        /// The logger to use to report from the Dispatcher.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>INeedACommandProcessor.</returns>
        public INeedACommandProcessor Logger(ILog logger)
        {
            this.logger = logger;
            return this;
        }

        /// <summary>
        /// The command processor used to send and publish messages to handlers by the service activator.
        /// </summary>
        /// <param name="theCommandProcessor">The command processor.</param>
        /// <returns>INeedAMessageMapper.</returns>
        public INeedAMessageMapper CommandProcessor(CommandProcessor theCommandProcessor)
        {
            this.commandProcessor = theCommandProcessor;
            return this;
        }

        /// <summary>
        /// The message mappers used to map between commands, events, and on-the-wire handlers.
        /// </summary>
        /// <param name="theMessageMapperRegistry">The message mapper registry.</param>
        /// <returns>INeedAChannelFactory.</returns>
        public INeedAChannelFactory MessageMappers(IAmAMessageMapperRegistry theMessageMapperRegistry)
        {
            this.messageMapperRegistry = theMessageMapperRegistry;
            return this;
        }

        /// <summary>
        /// The channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP 
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="channelFactory">The channel factory.</param>
        /// <returns>INeedAListOfConnections.</returns>
        public INeedAListOfConnections ChannelFactory(IAmAChannelFactory channelFactory)
        {
            this.channelFactory = channelFactory;
            return this;
        }

        /// <summary>
        /// A list of connections i.e. mappings of channels to commands or events
        /// </summary>
        /// <param name="connections">The connections.</param>
        /// <returns>IAmADispatchBuilder.</returns>
        public IAmADispatchBuilder Connections(IEnumerable<Connection> connections)
        {
            this.connections = connections;
            return this;
        }

        /// <summary>
        /// Obtains a list of connections i.e. mappings of channels to commands or events via the configuration file for the application
        /// </summary>
        /// <returns>IAmADispatchBuilder.</returns>
        public IAmADispatchBuilder  ConnectionsFromConfiguration()
        {
            var configuration = ServiceActivatorConfigurationSection.GetConfiguration();
            var connectionElements = configuration.Connections;
            var connectionFactory = new ConnectionFactory(channelFactory);
            return Connections(connectionFactory.Create(connectionElements));
        }


        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>Dispatcher.</returns>
        public Dispatcher Build()
        {
            return new Dispatcher(commandProcessor, messageMapperRegistry, connections, logger);
        }

    }

    #region Progressive Interfaces
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
        INeedAMessageMapper CommandProcessor(CommandProcessor commandProcessor);
    }

    /// <summary>
    /// Interface INeedAMessageMapper
    /// </summary>
    public interface INeedAMessageMapper
    {
        /// <summary>
        /// The message mappers used to map between commands, events, and on-the-wire handlers.
        /// </summary>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <returns>INeedAChannelFactory.</returns>
        INeedAChannelFactory MessageMappers(IAmAMessageMapperRegistry messageMapperRegistry);
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
        INeedAListOfConnections ChannelFactory(IAmAChannelFactory channelFactory);
    }

    /// <summary>
    /// Interface INeedAListOfConnections
    /// </summary>
    public interface INeedAListOfConnections
    {
        /// <summary>
        /// A list of connections i.e. mappings of channels to commands or events
        /// </summary>
        /// <returns>IAmADispatchBuilder.</returns>
       IAmADispatchBuilder ConnectionsFromConfiguration();
       /// <summary>
       /// Obtains a list of connections i.e. mappings of channels to commands or events via the configuration file for the application
       /// </summary>
       /// <param name="connections">The connections.</param>
       /// <returns>IAmADispatchBuilder.</returns>
       IAmADispatchBuilder Connections(IEnumerable<Connection> connections);
    }

    /// <summary>
    /// Interface IAmADispatchBuilder
    /// </summary>
    public interface IAmADispatchBuilder
    {
        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>Dispatcher.</returns>
        Dispatcher Build();
    }
    #endregion
}