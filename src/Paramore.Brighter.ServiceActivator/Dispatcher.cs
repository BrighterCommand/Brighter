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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.ServiceActivator.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Class Dispatcher.
    /// The 'core' Service Activator class, the Dispatcher controls and co-ordinates the creation of readers from channels, and dispatching the commands and
    /// events translated from those messages to handlers. It controls the lifetime of the application through <see cref="Receive"/> and <see cref="End"/> and allows
    /// the stop and start of individual connections through <see cref="Open"/> and <see cref="Shut"/>
    /// </summary>
    public class Dispatcher : IDispatcher
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<Dispatcher>);

        private Task _controlTask;
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private readonly ConcurrentDictionary<string, IAmAConsumer> _consumers;

        /// <summary>
        /// Gets the command processor.
        /// </summary>
        /// <value>The command processor.</value>
        public IAmACommandProcessor CommandProcessor { get; }

        /// <summary>
        /// Gets the connections.
        /// </summary>
        /// <value>The connections.</value>
        public IEnumerable<Connection> Connections { get; private set; }

        /// <summary>
        /// Gets the <see cref="Consumer"/>s
        /// </summary>
        /// <value>The consumers.</value>
        public IEnumerable<IAmAConsumer> Consumers => _consumers.Values;

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
        {
            CommandProcessor = commandProcessor;
            Connections = connections;
            _messageMapperRegistry = messageMapperRegistry;

            State = DispatcherState.DS_NOTREADY;

            _tasks = new ConcurrentDictionary<int, Task>();
            _consumers = new ConcurrentDictionary<string, IAmAConsumer>();

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
                _logger.Value.Info("Dispatcher: Stopping dispatcher");
                Consumers.Each(consumer => consumer.Shut());
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
            _logger.Value.InfoFormat("Dispatcher: Opening connection {0}", connection.Name);

            AddConnectionToConnections(connection);
            var addedConsumers = CreateConsumers(new[] { connection });

            switch (State)
            {
                case DispatcherState.DS_RUNNING:
                    addedConsumers.Each(consumer =>
                    {
                        _consumers.TryAdd(consumer.Name, consumer);
                        consumer.Open();
                        _tasks.TryAdd(consumer.JobId, consumer.Job);
                    });
                    break;
                case DispatcherState.DS_STOPPED:
                case DispatcherState.DS_AWAITING:
                    addedConsumers.Each(consumer => _consumers.TryAdd(consumer.Name, consumer));
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
            CreateConsumers(Connections).Each(consumer => _consumers.TryAdd(consumer.Name, consumer));
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
                _logger.Value.InfoFormat("Dispatcher: Stopping connection {0}", connection.Name);
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
                    _logger.Value.Info("Dispatcher: Dispatcher starting");
                    State = DispatcherState.DS_RUNNING;

                    var consumers = Consumers.ToArray();
                    consumers.Each(consumer => consumer.Open());
                    consumers.Each(consumer => _tasks.TryAdd(consumer.JobId, consumer.Job));

                    _logger.Value.InfoFormat("Dispatcher: Dispatcher starting {0} performers", _tasks.Count);

                    while (!_tasks.IsEmpty)
                    {
                        try
                        {
                            var runningTasks = _tasks.Values.ToArray();
                            var index = Task.WaitAny(runningTasks);
                            var stoppingConsumer = runningTasks[index];
                            _logger.Value.DebugFormat("Dispatcher: Performer stopped with state {0}", stoppingConsumer.Status);

                            var consumer = Consumers.SingleOrDefault(c => c.JobId == stoppingConsumer.Id);
                            if (consumer != null)
                            {
                                _logger.Value.DebugFormat("Dispatcher: Removing a consumer with connection name {0}", consumer.Name);
                                consumer.Dispose();

                                _consumers.TryRemove(consumer.Name, out consumer);
                            }

                            Task removedTask;
                            _tasks.TryRemove(stoppingConsumer.Id, out removedTask);
                        }
                        catch (AggregateException ae)
                        {
                            ae.Handle(ex =>
                            {
                                _logger.Value.ErrorFormat("Dispatcher: Error on consumer; consumer shut down");
                                return true;
                            });
                        }
                    }

                    State = DispatcherState.DS_STOPPED;
                    _logger.Value.Info("Dispatcher: Dispatcher stopped");
                }
            },
            TaskCreationOptions.LongRunning);
        }

        private IEnumerable<Consumer> CreateConsumers(IEnumerable<Connection> connections)
        {
            var list = new List<Consumer>();
            connections.Each(connection =>
            {
                for (var i = 0; i < connection.NoOfPeformers; i++)
                {
                    int performer = i;
                    _logger.Value.InfoFormat("Dispatcher: Creating consumer number {0} for connection: {1}", performer + 1, connection.Name);
                    var consumerFactoryType = typeof(ConsumerFactory<>).MakeGenericType(connection.DataType);
                    var consumerFactory = (IConsumerFactory)Activator.CreateInstance(consumerFactoryType, CommandProcessor, _messageMapperRegistry, connection);

                    list.Add(connection.IsAsync ? consumerFactory.CreateAsync() : consumerFactory.Create());
                }
            });
            return list;
        }
    }
}