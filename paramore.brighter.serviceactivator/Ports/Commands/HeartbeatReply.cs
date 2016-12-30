using System.Collections.Generic;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator.Ports.Commands
{
    public class HeartbeatReply : Reply
    {
        public HeartbeatReply(string hostName, ReplyAddress sendersAddress)
            :base(sendersAddress)
        {
            HostName = hostName;
            Consumers = new List<RunningConsumer>();
        }

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
