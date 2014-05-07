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
    public enum DispatcherState
    {
        DS_NOTREADY = 0,
        DS_AWAITING = 1,
        DS_RUNNING = 2,
        DS_STOPPED = 3
    }

    public class Dispatcher
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly ILog logger;
        private Task controlTask;
        private readonly IList<Task> tasks = new SynchronizedCollection<Task>(); 

        public IList<Consumer> Consumers { get; private set; }
        public DispatcherState State { get; private set; }

        public Dispatcher(IAdaptAnInversionOfControlContainer container, IEnumerable<Connection> connections, ILog logger)
        {
            this.container = container;
            this.logger = logger;
            State = DispatcherState.DS_NOTREADY;

            Consumers = new SynchronizedCollection<Consumer>();
            CreateConsumers(connections).Each(consumer => Consumers.Add(consumer));
            
            State = DispatcherState.DS_AWAITING;
            logger.Debug(m => m("Dispatcher is ready to recieve"));
        }

        public void Recieve()
        {
            controlTask = Task.Factory.StartNew(() =>
            {
                if (State == DispatcherState.DS_AWAITING || State == DispatcherState.DS_STOPPED)
                {
                    State = DispatcherState.DS_RUNNING;
                    logger.Debug(m => m("Dispatcher: Dispatcher starting"));

                    Consumers.Each((consumer) => consumer.Wake());

                    Consumers.Select(consumer => consumer.Job).Each(job => tasks.Add(job));

                    logger.Debug(m => m("Dispatcher: Dispatcher starting {0} performers", tasks.Count));

                    while (tasks.Any())
                    {
                        try
                        {
                            var index = Task.WaitAny(tasks.ToArray());
                            logger.Debug(m => m("Dispatcher: Performer stopped with state {0}", tasks[index].Status));
                            tasks.RemoveAt(index);
                        }
                        catch (AggregateException ae)
                        {
                            ae.Handle((ex) =>
                                {
                                    logger.Error(m => m("Dispatcher: Error on lamp; lamp switched off"));
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

        public Task End()
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                logger.Debug(m => m("Dispatcher: Stopping dispatcher"));
                Consumers.Each((consumer) => consumer.Sleep());
            }

            return controlTask;
        }

        public void Shut(Connection connection)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                logger.Debug(m => m("Dispatcher: Stopping connection {0}", connection.Name));
                Consumers.Where(consumer => consumer.Name == connection.Name).Each((consumer) => consumer.Sleep());
            }
        }

        public void Open(Connection connection)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                logger.Debug(m => m("Dispatcher: Opening connection {0}", connection.Name));
                var addedConsumers = CreateConsumers(new List<Connection>() {connection});
                addedConsumers.Each((consumer) =>
                {
                    Consumers.Add(consumer);
                    consumer.Wake();
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
                    list.Add(ConsumerFactory.Create(this.container, connection, this.logger));
                }
            });
            return list;
        }
    }
}