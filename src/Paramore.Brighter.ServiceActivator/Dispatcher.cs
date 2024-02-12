﻿#region Licence
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
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.ServiceActivator.Status;
using BindingFlags = System.Reflection.BindingFlags;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Class Dispatcher.
    /// The 'core' Service Activator class, the Dispatcher controls and co-ordinates the creation of readers from channels, and dispatching the commands and
    /// events translated from those messages to handlers. It controls the lifetime of the application through <see cref="Receive"/> and <see cref="End"/> and allows
    /// the stop and start of individual connections through <see cref="Open(string)"/> and <see cref="Shut(string)"/>
    /// </summary>
    public class Dispatcher : IDispatcher
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<Dispatcher>();

        private Task _controlTask;
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly IAmAMessageTransformerFactory _messageTransformerFactory;
        private readonly IAmAMessageMapperRegistryAsync _messageMapperRegistryAsync;
        private readonly IAmAMessageTransformerFactoryAsync _messageTransformerFactoryAsync;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private readonly ConcurrentDictionary<string, IAmAConsumer> _consumers;

        /// <summary>
        /// Gets the command processor.
        /// </summary>
        /// <value>The command processor.</value>
        public IAmACommandProcessor CommandProcessor { get => CommandProcessorFactory.Invoke().Get(); }
        
        /// <summary>
        /// 
        /// </summary>
        public Func<IAmACommandProcessorProvider> CommandProcessorFactory { get; }
        
        /// <summary>
        /// Gets the connections.
        /// TODO: Rename to Subscriptions in V10
        /// </summary>
        /// <value>The connections.</value>
        public IEnumerable<Subscription> Connections { get; private set; }

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
        public HostName HostName { get; set; } = new HostName($"Brighter{Guid.NewGuid()}");

        /// <summary>
        /// Gets the state of the <see cref="Dispatcher"/>
        /// </summary>
        /// <value>The state.</value>
        public DispatcherState State { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dispatcher"/> class.
        /// </summary>
        /// <param name="commandProcessorFactory">The command processor Factory.</param>
        /// <param name="subscriptions">The subscriptions.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="messageMapperRegistryAsync">Async message mapper registry.</param>
        /// <param name="messageTransformerFactory">Creates instances of Transforms</param>
        /// <param name="messageTransformerFactoryAsync">Creates instances of Transforms async</param>
        /// throws <see cref="ConfigurationException">You must provide at least one type of message mapper registry</see>
        public Dispatcher(Func<IAmACommandProcessorProvider> commandProcessorFactory,
            IEnumerable<Subscription> subscriptions,
            IAmAMessageMapperRegistry messageMapperRegistry = null,
            IAmAMessageMapperRegistryAsync messageMapperRegistryAsync = null, 
            IAmAMessageTransformerFactory messageTransformerFactory = null,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync= null)
        {
            CommandProcessorFactory = commandProcessorFactory;
            
            Connections = subscriptions;
            _messageMapperRegistry = messageMapperRegistry;
            _messageMapperRegistryAsync = messageMapperRegistryAsync;
            _messageTransformerFactory = messageTransformerFactory;
            _messageTransformerFactoryAsync = messageTransformerFactoryAsync;

            if (messageMapperRegistry is null && messageMapperRegistryAsync is null)
                throw new ConfigurationException("You must provide a message mapper registry or an async message mapper registry");

            State = DispatcherState.DS_NOTREADY;

            _tasks = new ConcurrentDictionary<int, Task>();
            _consumers = new ConcurrentDictionary<string, IAmAConsumer>();

            State = DispatcherState.DS_AWAITING;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dispatcher"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor we should use with the dispatcher (prefer to use Command Processor Provider for IoC Scope control</param>
        /// <param name="subscriptions">The subscriptions.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="messageMapperRegistryAsync">Async message mapper registry.</param>
        /// <param name="messageTransformerFactory">Creates instances of Transforms</param>
        /// <param name="messageTransformerFactoryAsync">Creates instances of Transforms async</param>
        /// throws <see cref="ConfigurationException">You must provide at least one type of message mapper registry</see>        
        public Dispatcher(IAmACommandProcessor commandProcessor,
            IEnumerable<Subscription> subscriptions,
            IAmAMessageMapperRegistry messageMapperRegistry = null,
            IAmAMessageMapperRegistryAsync messageMapperRegistryAsync = null, 
            IAmAMessageTransformerFactory messageTransformerFactory = null,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync= null)
            : this(() => 
                new CommandProcessorProvider(commandProcessor), subscriptions, messageMapperRegistry, messageMapperRegistryAsync, messageTransformerFactory, messageTransformerFactoryAsync)
        {
        }

        /// <summary>
        /// Stop listening to messages
        /// </summary>
        /// <returns>Task.</returns>
        public Task End()
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                s_logger.LogInformation("Dispatcher: Stopping dispatcher");
                Consumers.Each(consumer => consumer.Shut());
            }

            return _controlTask;
        }

        /// <summary>
        /// Opens the specified subscription by name 
        /// </summary>
        /// <param name="connectionName">The name of the subscription</param>
        public void Open(string connectionName)
        {
            Open(Connections.SingleOrDefault(c => c.Name == connectionName));
        }

        /// <summary>
        /// Opens the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        public void Open(Subscription subscription)
        {
            s_logger.LogInformation("Dispatcher: Opening subscription {ChannelName}", subscription.Name);

            AddConnectionToConnections(subscription);
            var addedConsumers = CreateConsumers(new[] { subscription });

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

        private void AddConnectionToConnections(Subscription subscription)
        {
            if (Connections.All(c => c.Name != subscription.Name))
            {
                Connections = new List<Subscription>(Connections) { subscription };
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
        /// Shuts the specified subscription by name
        /// </summary>
        /// <param name="connectionName">The name of the subscription</param>
        public void Shut(string connectionName)
        {
            Shut(Connections.SingleOrDefault(c => c.Name == connectionName));
        }

        /// <summary>
        /// Shuts the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        public void Shut(Subscription subscription)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                s_logger.LogInformation("Dispatcher: Stopping subscription {ChannelName}", subscription.Name);
                var consumersForConnection = Consumers.Where(consumer => consumer.SubscriptionName == subscription.Name).ToArray();
                var noOfConsumers = consumersForConnection.Length;
                for (int i = 0; i < noOfConsumers; ++i)
                {
                    consumersForConnection[i].Shut();
                }
            }
        }

        public DispatcherStateItem[] GetState()
        {
            return Connections.Select(s => new DispatcherStateItem()
            {
                Name = s.Name,
                ExpectPerformers = s.NoOfPerformers,
                Performers = _consumers.Where(c => c.Value.SubscriptionName == s.Name).Select(c => new PerformerInformation()
                {
                    Name = c.Value.Name,
                    State = c.Value.State
                }).ToArray()
            }).ToArray();
        }

        public void SetActivePerformers(string connectionName, int numberOfPerformers)
        {
            var subscription = Connections.SingleOrDefault(c => c.Name == connectionName);
            var currentPerformers = subscription.NoOfPerformers;
            if(currentPerformers == numberOfPerformers)
                return;

            subscription.SetNumberOfPerformers(numberOfPerformers);
            if (currentPerformers < numberOfPerformers)
            {
                for (var i = currentPerformers; i < numberOfPerformers; i++)
                {
                    var consumer = CreateConsumer(subscription, i);
                    _consumers.TryAdd(consumer.Name, consumer);
                    consumer.Open();
                    _tasks.TryAdd(consumer.JobId, consumer.Job);
                }
            }
            else
            {
                var consumersForConnection = Consumers.Where(consumer => consumer.SubscriptionName == subscription.Name)
                    .ToArray();
                var consumersToClose = currentPerformers - numberOfPerformers;
                for (int i = 0; i < consumersToClose; ++i)
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
                    s_logger.LogInformation("Dispatcher: Dispatcher starting");
                    State = DispatcherState.DS_RUNNING;

                    var consumers = Consumers.ToArray();
                    consumers.Each(consumer => consumer.Open());
                    consumers.Each(consumer => _tasks.TryAdd(consumer.JobId, consumer.Job));

                    s_logger.LogInformation("Dispatcher: Dispatcher starting {Consumers} performers", _tasks.Count);

                    while (_tasks.Any())
                    {
                        try
                        {
                            var runningTasks = _tasks.Values.ToArray();
                            var index = Task.WaitAny(runningTasks);
                            var stoppingConsumer = runningTasks[index];
                            s_logger.LogDebug("Dispatcher: Performer stopped with state {Status}", stoppingConsumer.Status);

                            var consumer = Consumers.SingleOrDefault(c => c.JobId == stoppingConsumer.Id);
                            if (consumer != null)
                            {
                                s_logger.LogDebug("Dispatcher: Removing a consumer with subscription name {ChannelName}", consumer.Name);

                                if (_consumers.TryRemove(consumer.Name, out consumer))
                                {
                                    consumer.Dispose();
                                }
                            }

                            if (_tasks.TryRemove(stoppingConsumer.Id, out Task removedTask))
                            {
                                removedTask.Dispose();
                            }

                            stoppingConsumer.Dispose();
                        }
                        catch (AggregateException ae)
                        {
                            ae.Handle(ex =>
                            {
                                s_logger.LogError(ex, "Dispatcher: Error on consumer; consumer shut down");
                                return true;
                            });
                        }
                    }

                    State = DispatcherState.DS_STOPPED;
                    s_logger.LogInformation("Dispatcher: Dispatcher stopped");
                }
            },
            TaskCreationOptions.LongRunning);

            while (State != DispatcherState.DS_RUNNING)
            {
                Task.Delay(100).Wait();
            }
        }

        private IEnumerable<Consumer> CreateConsumers(IEnumerable<Subscription> subscriptions)
        {
            var list = new List<Consumer>();
            subscriptions.Each(subscription =>
            {
                for (var i = 0; i < subscription.NoOfPerformers; i++)
                {
                    list.Add(CreateConsumer(subscription, i + 1));
                }
            });
            return list;
        }
        
        private Consumer CreateConsumer(Subscription subscription, int consumerNumber)
        {
            s_logger.LogInformation("Dispatcher: Creating consumer number {ConsumerNumber} for subscription: {ChannelName}", consumerNumber, subscription.Name);
            var consumerFactoryType = typeof(ConsumerFactory<>).MakeGenericType(subscription.DataType);
            if (!subscription.RunAsync)
            {
                var types = new Type[]
                {
                    typeof(IAmACommandProcessorProvider), typeof(Subscription),  typeof(IAmAMessageMapperRegistry),typeof(IAmAMessageTransformerFactory)
                };
                
                var consumerFactoryCtor = consumerFactoryType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null,
                    CallingConventions.HasThis, types, null
                );
                    
                var consumerFactory = (IConsumerFactory)consumerFactoryCtor?.Invoke(new object[] { CommandProcessorFactory.Invoke(), subscription, _messageMapperRegistry,  _messageTransformerFactory });   

                return consumerFactory.Create();
            }
            else
            {
               
                 var types = new Type[]
                 {
                     typeof(IAmACommandProcessorProvider),typeof(Subscription),  typeof(IAmAMessageMapperRegistryAsync), typeof(IAmAMessageTransformerFactoryAsync)
                 };
                
                 var consumerFactoryCtor = consumerFactoryType.GetConstructor(
                         BindingFlags.Instance | BindingFlags.Public, null,
                         CallingConventions.HasThis, types, null
                     );
                     
                 var consumerFactory = (IConsumerFactory)consumerFactoryCtor?.Invoke(new object[] { CommandProcessorFactory.Invoke(),  subscription, _messageMapperRegistryAsync, _messageTransformerFactoryAsync });  

                return consumerFactory.Create();
            }
        }
    }
}
