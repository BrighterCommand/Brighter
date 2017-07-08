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
using System.Linq;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Class DispatchBuilder.
    /// A fluent builder used to simplify construction of instances of the Dispatcher. Begin by calling With() and the syntax will then provide you with 
    /// progressive interfaces to manage the requirements for a complete Dispatcher via Intellisense in the IDE. The intent is to make it easier to
    /// recognize those dependencies that you need to configure
    /// </summary>
    public class DispatchBuilder : INeedACommandProcessor, INeedAChannelFactory, INeedAMessageMapper, INeedAListOfConnections, IAmADispatchBuilder
    {
        private CommandProcessor _commandProcessor;
        private IAmAMessageMapperRegistry _messageMapperRegistry;
        private IAmAChannelFactory _defaultChannelFactory;
        private IEnumerable<ConnectionBase> _connections;

        private DispatchBuilder() { }

        /// <summary>
        /// Begins the fluent interface 
        /// </summary>
        /// <returns>INeedALogger.</returns>
        public static INeedACommandProcessor With()
        {
            return new DispatchBuilder();
        }

        /// <summary>
        /// The command processor used to send and publish messages to handlers by the service activator.
        /// </summary>
        /// <param name="theCommandProcessor">The command processor.</param>
        /// <returns>INeedAMessageMapper.</returns>
        public INeedAMessageMapper CommandProcessor(CommandProcessor theCommandProcessor)
        {
            _commandProcessor = theCommandProcessor;
            return this;
        }

        /// <summary>
        /// The message mappers used to map between commands, events, and on-the-wire handlers.
        /// </summary>
        /// <param name="theMessageMapperRegistry">The message mapper registry.</param>
        /// <returns>INeedAChannelFactory.</returns>
        public INeedAChannelFactory MessageMappers(IAmAMessageMapperRegistry theMessageMapperRegistry)
        {
            _messageMapperRegistry = theMessageMapperRegistry;
            return this;
        }

        /// <summary>
        /// The default channel factory - used to create channels. Generally an implementation of a specific Application Layer i.e.RabbitMQ for AMQP 
        /// needs to provide an implementation of this factory to provide input and output channels that support sending messages over that
        /// layer. We provide an implementation for RabbitMQ for example.
        /// </summary>
        /// <param name="channelFactory">The channel factory.</param>
        /// <returns>INeedAListOfConnections.</returns>
        public INeedAListOfConnections DefaultChannelFactory(IAmAChannelFactory channelFactory)
        {
            _defaultChannelFactory = channelFactory;
            return this;
        }

        /// <summary>
        /// A list of connections i.e. mappings of channels to commands or events
        /// </summary>
        /// <param name="connections">The connections.</param>
        /// <returns>IAmADispatchBuilder.</returns>
        public IAmADispatchBuilder Connections(IEnumerable<ConnectionBase> connections)
        {
            _connections = connections;

            foreach (var connection in _connections.Where(c => c.ChannelFactory == null))
            {
                connection.ChannelFactory = _defaultChannelFactory;
            }

            return this;
        }

        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns>Dispatcher.</returns>
        public Dispatcher Build()
        {
            return new Dispatcher(_commandProcessor, _messageMapperRegistry, _connections);
        }
    }

    #region Progressive Interfaces

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
        INeedAListOfConnections DefaultChannelFactory(IAmAChannelFactory channelFactory);
    }

    /// <summary>
    /// Interface INeedAListOfConnections
    /// </summary>
    public interface INeedAListOfConnections
    {
        ///// <summary>
        ///// A list of connections i.e. mappings of channels to commands or events
        ///// </summary>
        ///// <param name="connections"></param>
        ///// <returns>IAmADispatchBuilder.</returns>
        IAmADispatchBuilder Connections(IEnumerable<ConnectionBase> connections);
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