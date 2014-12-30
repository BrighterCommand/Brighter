// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
// <copyright file="Dispatcher.cs" company="">
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
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.extensions;

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
        private readonly IAmACommandProcessor commandProcessor;
        private readonly IAmAMessageMapperRegistry messageMapperRegistry;
        private readonly ILog logger;
        private Task controlTask;
        private readonly IList<Task> tasks = new SynchronizedCollection<Task>();

        /// <summary>
        /// Gets the <see cref="Consumer"/>s
        /// </summary>
        /// <value>The consumers.</value>
        public IList<Consumer> Consumers { get; private set; }
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
        /// <param name="logger">The logger.</param>
        public Dispatcher(IAmACommandProcessor commandProcessor, IAmAMessageMapperRegistry messageMapperRegistry, IEnumerable<Connection> connections, ILog logger)
        {
            this.commandProcessor = commandProcessor;
            this.messageMapperRegistry = messageMapperRegistry;
            this.logger = logger;
            State = DispatcherState.DS_NOTREADY;

            Consumers = new SynchronizedCollection<Consumer>();
            CreateConsumers(connections).Each(consumer => Consumers.Add(consumer));
            
            State = DispatcherState.DS_AWAITING;
            logger.Debug(m => m("Dispatcher is ready to recieve"));
        }

        /// <summary>
        /// Begins listening for messages on channels, and dispatching them to request handlers.
        /// </summary>
        public void Receive()
        {
            controlTask = Task.Factory.StartNew(() =>
            {
                if (State == DispatcherState.DS_AWAITING || State == DispatcherState.DS_STOPPED)
                {
                    State = DispatcherState.DS_RUNNING;
                    logger.Debug(m => m("Dispatcher: Dispatcher starting"));

                    Consumers.Each((consumer) => consumer.Open());

                    Consumers.Select(consumer => consumer.Job).Each(job => tasks.Add(job));

                    logger.Debug(m => m("Dispatcher: Dispatcher starting {0} performers", tasks.Count));

                    while (tasks.Any())
                    {
                        try
                        {
                            var index = Task.WaitAny(tasks.ToArray());
                            //TODO: This doesn't really identify the connection that we closed - which is what we want diagnostically
                            logger.Debug(m => m("Dispatcher: Performer stopped with state {0}", tasks[index].Status));

                            var consumer = Consumers.SingleOrDefault(c => c.JobId == tasks[index].Id);
                            if (consumer != null)
                            {
                                logger.Debug(m => m("Dispatcher: Removing a consumer with connection name {0}", consumer.Name));
                                consumer.Dispose();
                                Consumers.Remove(consumer);
                            }

                            tasks[index].Dispose();
                            tasks.RemoveAt(index);
                        }
                        catch (AggregateException ae)
                        {
                            ae.Handle((ex) =>
                                {
                                    logger.Error(m => m("Dispatcher: Error on consumer; consumer shut down"));
                                    return true;
                                });
                        }
                    }

                    State = DispatcherState.DS_STOPPED;
                    logger.Debug(m => m("Dispatcher: Dispatcher stopped"));
                }
            },
            TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Stop listening to messages
        /// </summary>
        /// <returns>Task.</returns>
        public Task End()
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                logger.Debug(m => m("Dispatcher: Stopping dispatcher"));
                Consumers.Each((consumer) => consumer.Shut());
            }

            return controlTask;
        }

        /// <summary>
        /// Shuts the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public void Shut(Connection connection)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                logger.Debug(m => m("Dispatcher: Stopping connection {0}", connection.Name));
                Consumers.Where(consumer => consumer.Name == connection.Name).Each((consumer) => consumer.Shut());
            }
        }

        /// <summary>
        /// Opens the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <exception cref="System.InvalidOperationException">Use Recieve to start the dispatcher; use Open to start a shut or added connection</exception>
        public void Open(Connection connection)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                logger.Debug(m => m("Dispatcher: Opening connection {0}", connection.Name));
                var addedConsumers = CreateConsumers(new List<Connection>() {connection});
                addedConsumers.Each((consumer) =>
                {
                    Consumers.Add(consumer);
                    consumer.Open();
                    tasks.Add(consumer.Job);
                } );
            }
            else
            {
                throw new InvalidOperationException("Use Recieve to start the dispatcher; use Open to start a shut or added connection");
            }
        }

        private IEnumerable<Consumer> CreateConsumers(IEnumerable<Connection> connections)
        {
            var list = new List<Consumer>();
            connections.Each((connection) =>
            {
                for (var i = 0; i < connection.NoOfPeformers; i++)
                {
                    int performer = i;
                    logger.Debug(m => m("Dispatcher: Creating performer {0} for connection: {1}", performer, connection.Name));
                    var consumerFactoryType = typeof (ConsumerFactory<>).MakeGenericType(connection.DataType);
                    var consumerFactory = (IConsumerFactory) Activator.CreateInstance(consumerFactoryType, new object[]{commandProcessor, messageMapperRegistry, connection, logger });
  
                    list.Add(consumerFactory.Create());
                }
            });
            return list;
        }
    }
}