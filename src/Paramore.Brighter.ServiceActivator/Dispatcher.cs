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
using System.Threading;
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

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly ConcurrentDictionary<string, ConnectionBase> _connections;
        private readonly ConcurrentDictionary<IAmAConsumer, Task> _consumers;

        /// <summary>
        /// Gets the command processor.
        /// </summary>
        /// <value>The command processor.</value>
        public IAmACommandProcessor CommandProcessor { get; }

        /// <summary>
        /// Gets the connections.
        /// </summary>
        /// <value>The connections.</value>
        public IEnumerable<ConnectionBase> Connections => _connections.Values;

        /// <summary>
        /// Gets the <see cref="Consumer"/>s
        /// </summary>
        /// <value>The consumers.</value>
        public IEnumerable<IAmAConsumer> Consumers => _consumers.Keys;

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
            IEnumerable<ConnectionBase> connections)
        {
            State = DispatcherState.DS_NOTREADY;

            CommandProcessor = commandProcessor;

            _messageMapperRegistry = messageMapperRegistry;
            _cancellationTokenSource = new CancellationTokenSource();
            _connections = new ConcurrentDictionary<string, ConnectionBase>(connections.Select(c => new KeyValuePair<string, ConnectionBase>(c.Name, c)));
            _consumers = new ConcurrentDictionary<IAmAConsumer, Task>();

            State = DispatcherState.DS_AWAITING;
        }

        /// <summary>
        /// Stop listening to messages
        /// </summary>
        /// <returns>Task.</returns>
        public async Task End()
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                _logger.Value.Info("Dispatcher: Stopping dispatcher");

                _cancellationTokenSource.Cancel();

                try
                {
                    await Task.WhenAll(_consumers.Values);
                }
                catch (TaskCanceledException)
                {
                    // ignore this race
                }

                _consumers.Clear();

                State = DispatcherState.DS_STOPPED;

                _logger.Value.Info("Dispatcher: Dispatcher stopped");
            }
        }

        /// <summary>
        /// Opens the specified connection by name 
        /// </summary>
        /// <param name="connectionName">The name of the connection</param>
        public void Open(string connectionName)
        {
            if (_connections.TryGetValue(connectionName, out ConnectionBase connection))
            {
                Open(connection);
            }
        }

        /// <summary>
        /// Opens the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public void Open(ConnectionBase connection)
        {
            _logger.Value.InfoFormat("Dispatcher: Opening connection {0}", connection.Name);

            if (!_connections.ContainsKey(connection.Name))
            {
                _connections.TryAdd(connection.Name, connection);
            }

            var addedConsumers = CreateConsumers(new[] { connection });

            switch (State)
            {
                case DispatcherState.DS_RUNNING:
                    foreach (var consumer in addedConsumers)
                    {
                        _consumers.TryAdd(consumer, Task.CompletedTask);
                        StartConsumer(consumer);
                    }
                    break;

                case DispatcherState.DS_STOPPED:
                case DispatcherState.DS_AWAITING:
                    foreach (var consumer in addedConsumers)
                    {
                        _consumers.TryAdd(consumer, Task.CompletedTask);
                    }
                    Start();
                    break;

                default:
                    throw new InvalidOperationException("The dispatcher is not ready");
            }
        }

        /// <summary>
        /// Begins listening for messages on channels, and dispatching them to request handlers.
        /// </summary>
        public void Receive()
        {
            if (State == DispatcherState.DS_AWAITING)
            {
                _logger.Value.Info("Dispatcher: Starting dispatcher");

                foreach (var consumer in CreateConsumers(Connections))
                {
                    _consumers.TryAdd(consumer, Task.CompletedTask);
                }

                Start();
            }
        }

        /// <summary>
        /// Shuts the specified connection by name
        /// </summary>
        /// <param name="connectionName">The name of the connection</param>
        public void Shut(string connectionName)
        {
            if (_connections.TryGetValue(connectionName, out ConnectionBase connection))
            {
                Shut(connection);
            }
        }

        /// <summary>
        /// Shuts the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public void Shut(ConnectionBase connection)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                _logger.Value.InfoFormat("Dispatcher: Stopping connection {0}", connection.Name);

                var consumersForConnection = Consumers.Where(consumer => consumer.Name == connection.Name).ToArray();
                var tasks = new List<Task>(consumersForConnection.Length);
                foreach (var consumer in consumersForConnection)
                {
                    consumer.Shut();

                    if (_consumers.TryRemove(consumer, out Task task))
                    {
                        tasks.Add(task);
                    }
                }

                // Task.WhenAll(tasks).GetAwaiter().GetResult(); todo probably want to do this, and probably with await
            }
        }

        private void Start()
        {
            if (State == DispatcherState.DS_AWAITING || State == DispatcherState.DS_STOPPED)
            {
                _logger.Value.Info("Dispatcher: Dispatcher starting");

                State = DispatcherState.DS_RUNNING;

                _logger.Value.InfoFormat("Dispatcher: Dispatcher starting {0} performers", _consumers.Count);

                foreach (var consumer in Consumers)
                {
                    StartConsumer(consumer);
                }
            }
        }

        private void StartConsumer(IAmAConsumer consumer)
        {
            var task = consumer.Open(_cancellationTokenSource.Token);
            if (!_consumers.TryUpdate(consumer, task, Task.CompletedTask))
                throw new InvalidOperationException("Tried to start a consumer that was not currently stopped. This should never happen, this exception is just here to make debugging easier.");

            _cancellationTokenSource.Token.Register(() =>
            {
                consumer.Shut();
                consumer.Dispose();

                _logger.Value.DebugFormat("Dispatcher: Performer stopped with state {0}", consumer.State);
            });
        }

        private IEnumerable<Consumer> CreateConsumers(IEnumerable<ConnectionBase> connections)
        {
            foreach (var connection in connections)
            {
                for (var i = 0; i < connection.NoOfPeformers; i++)
                {
                    _logger.Value.InfoFormat("Dispatcher: Creating consumer number {0} for connection: {1}", i + 1, connection.Name);

                    var consumerFactoryType = typeof(ConsumerFactory<>).MakeGenericType(connection.DataType);
                    var consumerFactory = (IConsumerFactory)Activator.CreateInstance(consumerFactoryType, CommandProcessor, _messageMapperRegistry, connection);

                    yield return consumerFactory.Create();
                }
            }
        }
    }
}