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
        private readonly ILog logger;
        private readonly List<Consumer> consumers = new List<Consumer>();
        private Task controlTask;

        public Dispatcher(IAdaptAnInversionOfControlContainer container, IEnumerable<Connection> connections, ILog logger)
        {
            this.logger = logger;
            State = DispatcherState.DS_NOTREADY;
            connections.Each((connection) =>
                {
                    for (var i = 0; i < connection.NoOfPeformers; i++)
                        consumers.Add(ConsumerFactory.Create(container, connection));
                });
            State = DispatcherState.DS_AWAITING;
        }

        public DispatcherState State { get; private set; }

        public void Recieve()
        {
            controlTask = Task.Factory.StartNew(() =>
            {
                if (State == DispatcherState.DS_AWAITING || State == DispatcherState.DS_STOPPED)
                {
                    State = DispatcherState.DS_RUNNING;

                    consumers.Each((consumer) => consumer.Wake());

                    var tasks = consumers.Select(lamp => lamp.Job).ToList();

                    while (tasks.Any())
                    {
                        try
                        {
                            var index = Task.WaitAny(tasks.ToArray());
                            tasks.RemoveAt(index);
                        }
                        catch (AggregateException ae)
                        {
                            ae.Handle((ex) =>
                                {
                                    logger.Error(m => m("Error on lamp; lamp switched off"));
                                    return true;
                                });
                        }
                    }

                    State = DispatcherState.DS_STOPPED;
                }
            },
            TaskCreationOptions.LongRunning);
        }

        public Task End()
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                consumers.Each((consumer) => consumer.Sleep());
            }

            return controlTask;
        }
    }
}