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

    public class DispatchBuilder : INeedAnInversionOfControlContainer, INeedALogger, INeedACommandProcessor, INeedAChannelFactory, INeedAListOfConnections, IAmADispatchBuilder
    {
        private IAdaptAnInversionOfControlContainer container;
        private IAmAChannelFactory channelFactory;
        private IEnumerable<Connection> connections;
        private ILog logger;
        private DispatchBuilder() {}


        public static INeedAnInversionOfControlContainer With()
        {
            return new DispatchBuilder();
        }

        public INeedALogger InversionOfControl(IAdaptAnInversionOfControlContainer theContainer)
        {
            this.container = theContainer;
            return this;
        }

        public INeedACommandProcessor WithLogger(ILog logger)
        {
            this.logger = logger;
            container.Register<ILog, ILog>(logger);
            return this;
        }

        public INeedAChannelFactory WithCommandProcessor(CommandProcessor commandProcessor)
        {
            container.Register<IAmACommandProcessor, IAmACommandProcessor>(commandProcessor);
            return this;
        }

        public INeedAListOfConnections WithChannelFactory(IAmAChannelFactory channelFactory)
        {
            this.channelFactory = channelFactory;
            return this;
        }

        public IAmADispatchBuilder Connections(IEnumerable<Connection> connections)
        {
            this.connections = connections;
            return this;
        }

        public IAmADispatchBuilder  ConnectionsFromConfiguration()
        {
            var configuration = ServiceActivatorConfigurationSection.GetConfiguration();
            var connectionElements = configuration.Connections;
            var connectionFactory = new ConnectionFactory(channelFactory);
            return Connections(connectionFactory.Create(connectionElements));
        }


        public Dispatcher Build()
        {
            return new Dispatcher(container, connections, logger);
        }

    }

    #region Progressive Interfaces
    public interface INeedAnInversionOfControlContainer
    {
        INeedALogger InversionOfControl(IAdaptAnInversionOfControlContainer theContainer);
    }

    public interface INeedALogger
    {
        INeedACommandProcessor WithLogger(ILog logger);
    }

    public interface INeedACommandProcessor
    {
        INeedAChannelFactory WithCommandProcessor(CommandProcessor commandProcessor);
    }

    public interface INeedAChannelFactory
    {
        INeedAListOfConnections WithChannelFactory(IAmAChannelFactory channelFactory);
    }

    public interface INeedAListOfConnections
    {
       IAmADispatchBuilder ConnectionsFromConfiguration();
       IAmADispatchBuilder Connections(IEnumerable<Connection> connections);
    }

    public interface IAmADispatchBuilder
    {
        Dispatcher Build();
    }
    #endregion
}