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
            });
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