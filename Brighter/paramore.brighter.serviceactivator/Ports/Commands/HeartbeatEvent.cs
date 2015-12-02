using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator.Ports.Commands
{
    public class HeartbeatEvent : Event
    {
        public HeartbeatEvent(string hostName)
            :this(Guid.NewGuid())
        {
            HostName = hostName;
            Consumers = new List<RunningConsumer>();
        }

        public HeartbeatEvent(Guid id) : base(id) {}
        public string HostName { get; private set; }
        public IList<RunningConsumer> Consumers { get; private set; }
    }

    public class RunningConsumer
    {
        public RunningConsumer(ConnectionName connectionName, ConsumerState state)
        {
            ConnectionName = connectionName;
            State = state;
        }

        public ConnectionName ConnectionName { get; private set; }
        public ConsumerState State { get; private set; }
    }
}
