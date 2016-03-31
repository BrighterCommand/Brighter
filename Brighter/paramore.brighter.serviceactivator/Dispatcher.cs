


// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.serviceactivator
{
    /// <summary>
    /// Class Dispatcher.
    /// The 'core' Service Activator class, the Dispatcher controls and co-ordinates the creation of readers from channels, and dispatching the commands and
    /// events translated from those messages to handlers. It controls the lifetime of the application through <see cref="Receive"/> and <see cref="End"/> and allows
    /// the stop and start of individual connections through <see cref="Open"/> and <see cref="Shut"/>
    /// </summary>
    public class Dispatcher : IDispatcher
    {
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly ILog _logger;
        private Task _controlTask;
        private readonly IList<Task> _tasks = new SynchronizedCollection<Task>();

        /// <summary>
        /// Gets the command processor.
        /// </summary>
        /// <value>The command processor.</value>
        public IAmACommandProcessor CommandProcessor { get; private set; }

        /// <summary>
        /// Gets the connections.
        /// </summary>
        /// <value>The connections.</value>
        public IEnumerable<Connection> Connections { get; private set; }

        /// <summary>
        /// Gets the <see cref="Consumer"/>s
        /// </summary>
        /// <value>The consumers.</value>
        public IList<IAmAConsumer> Consumers { get; private set; }

        /// <summary>
        /// Gets or sets the name for this dispatcher instance.
        /// Used when communicating with this instance via the Control Bus
        /// </summary>
        /// <value>The name of the host.</value>
        public HostName HostName { get; set; }

        /// <summary>
        /// Gets the state of the <see cref="Dispatcher"/>
        /// </summary>
        /// <value>The state.</value>
        public DispatcherState State { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dispatcher"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="connections">The connections.</param>
        public Dispatcher(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapperRegistry messageMapperRegistry,
            IEnumerable<Connection> connections)
            :this(commandProcessor, messageMapperRegistry, connections, LogProvider.GetCurrentClassLogger())
        {}


        /// <summary>
        /// Initializes a new instance of the <see cref="Dispatcher"/> class.
        /// Use this if you need to inject a logger, for example for testing
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="connections">The connections.</param>
        /// <param name="logger">The logger.</param>
        public Dispatcher(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapperRegistry messageMapperRegistry, 
            IEnumerable<Connection> connections,
            ILog logger)
        {
            CommandProcessor = commandProcessor;
            _messageMapperRegistry = messageMapperRegistry;
            this.Connections = connections;
            _logger = logger;
            State = DispatcherState.DS_NOTREADY;

            Consumers = new SynchronizedCollection<IAmAConsumer>();

            State = DispatcherState.DS_AWAITING;
        }

        /// <summary>
        /// Stop listening to messages
        /// </summary>
        /// <returns>Task.</returns>
        public Task End()
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                _logger.Info("Dispatcher: Stopping dispatcher");
                Consumers.Each((consumer) => consumer.Shut());
            }

            return _controlTask;
        }

        /// <summary>
        /// Opens the specified connection by name 
        /// </summary>
        /// <param name="connectionName">The name of the connection</param>
        public void Open(string connectionName)
        {
            Open(Connections.SingleOrDefault(c => c.Name == connectionName));
        }

        /// <summary>
        /// Opens the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public void Open(Connection connection)
        {
            _logger.InfoFormat("Dispatcher: Opening connection {0}", connection.Name);

            AddConnectionToConnections(connection);
            var addedConsumers = CreateConsumers(new[] { connection });

            switch (State)
            {
                case DispatcherState.DS_RUNNING:
                    addedConsumers.Each(
                        (consumer) =>
                        {
                            Consumers.Add(consumer);
                            consumer.Open();
                            _tasks.Add(consumer.Job);
                        });
                    break;
                case DispatcherState.DS_STOPPED:
                case DispatcherState.DS_AWAITING:
                    addedConsumers.Each((consumer) => Consumers.Add(consumer));
                    Start();
                    break;
                default:
                    throw new InvalidOperationException("The dispatcher is not ready");
            }
        }

        private void AddConnectionToConnections(Connection connection)
        {
            if (Connections.All(c => c.Name != connection.Name))
            {
                Connections = new List<Connection>(Connections) { connection };
            }
        }

        /// <summary>
        /// Begins listening for messages on channels, and dispatching them to request handlers.
        /// </summary>
        public void Receive()
        {
            CreateConsumers(Connections).Each(consumer => Consumers.Add(consumer));
            Start();
        }

        /// <summary>
        /// Shuts the specified connection by name
        /// </summary>
        /// <param name="connectionName">The name of the connection</param>
        public void Shut(string connectionName)
        {
            Shut(Connections.SingleOrDefault(c => c.Name == connectionName));
        }

        /// <summary>
        /// Shuts the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public void Shut(Connection connection)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                _logger.InfoFormat("Dispatcher: Stopping connection {0}", connection.Name);
                var consumersForConnection = Consumers.Where(consumer => consumer.Name == connection.Name).ToArray();
                var noOfConsumers = consumersForConnection.Length;
                for (int i = 0; i < noOfConsumers; ++i)
                {
                    consumersForConnection[i].Shut();
                }
            }
        }

        private void Start()
        {
            _controlTask = Task.Factory.StartNew(() =>
                {
                    if (State == DispatcherState.DS_AWAITING || State == DispatcherState.DS_STOPPED)
                    {
                        _logger.Info("Dispatcher: Dispatcher starting");
                        State = DispatcherState.DS_RUNNING;

                        var consumers = Consumers.ToArray();
                        consumers.Each((consumer) => consumer.Open());
                        consumers.Select(consumer => consumer.Job).Each(job => _tasks.Add(job));

                        _logger.InfoFormat("Dispatcher: Dispatcher starting {0} performers", _tasks.Count);

                        while (_tasks.Any())
                        {
                            try
                            {
                                var index = Task.WaitAny(_tasks.ToArray());
                                _logger.DebugFormat("Dispatcher: Performer stopped with state {0}", _tasks[index].Status);

                                var consumer = Consumers.SingleOrDefault(c => c.JobId == _tasks[index].Id);
                                if (consumer != null)
                                {
                                    _logger.DebugFormat("Dispatcher: Removing a consumer with connection name {0}", consumer.Name);
                                    consumer.Dispose();
                                    Consumers.Remove(consumer);
                                }

                                _tasks[index].Dispose();
                                _tasks.RemoveAt(index);
                            }
                            catch (AggregateException ae)
                            {
                                ae.Handle(
                                    (ex) =>
                                    {
                                        _logger.ErrorFormat("Dispatcher: Error on consumer; consumer shut down");
                                        return true;
                                    });
                            }
                        }

                        State = DispatcherState.DS_STOPPED;
                        _logger.Info("Dispatcher: Dispatcher stopped");
                    }
                },
                TaskCreationOptions.LongRunning);
        }

        private IEnumerable<Consumer> CreateConsumers(IEnumerable<Connection> connections)
        {
            var list = new List<Consumer>();
            connections.Each((connection) =>
            {
                for (var i = 0; i < connection.NoOfPeformers; i++)
                {
                    int performer = i;
                    _logger.InfoFormat("Dispatcher: Creating consumer number {0} for connection: {1}", (performer + 1), connection.Name);
                    var consumerFactoryType = typeof(ConsumerFactory<>).MakeGenericType(connection.DataType);
                    var consumerFactory = (IConsumerFactory)Activator.CreateInstance(consumerFactoryType, new object[] { CommandProcessor, _messageMapperRegistry, connection, _logger });

                    list.Add(connection.IsAsync ? consumerFactory.CreateAsync() : consumerFactory.Create());
                }
            });
            return list;
        }
    }
}